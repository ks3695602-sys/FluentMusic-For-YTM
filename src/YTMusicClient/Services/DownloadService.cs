using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YTMusicClient.Helpers;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public class DownloadService : IDownloadService
{
    private readonly YoutubeClient _youtube;
    private readonly HttpClient _httpClient;
    private readonly List<Song> _downloadedSongs = new();
    private const string ManifestFileName = "downloads_manifest.json";
    private bool _isLoaded;

    public event EventHandler? DownloadedSongsChanged;

    public DownloadService()
    {
        _youtube = new YoutubeClient();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;
        _isLoaded = true;

        try
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            var item = await localFolder.TryGetItemAsync(ManifestFileName);
            if (item is StorageFile file)
            {
                var json = await FileIO.ReadTextAsync(file);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = JsonSerializer.Deserialize<List<Song>>(json);
                    if (list != null)
                    {
                        _downloadedSongs.Clear();
                        foreach (var s in list)
                        {
                            if (!string.IsNullOrEmpty(s.LocalFilePath) && File.Exists(s.LocalFilePath))
                            {
                                _downloadedSongs.Add(s);
                            }
                        }
                    }
                }
            }

            // Real Filesystem Auto-Scanner: Scan disk directories for downloaded songs
            var scanDirs = new List<string>();

            // 1. App local downloads folder
            var appDownloads = Path.Combine(localFolder.Path, "Downloads");
            if (Directory.Exists(appDownloads)) scanDirs.Add(appDownloads);

            // 2. User Music/FluentMusic/Downloads folder
            var musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var userDownloads = Path.Combine(musicFolder, "FluentMusic", "Downloads");
            if (Directory.Exists(userDownloads)) scanDirs.Add(userDownloads);

            var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".m4a", ".opus", ".mp3", ".flac", ".webm", ".wav" };

            foreach (var dir in scanDirs)
            {
                foreach (var filePath in Directory.GetFiles(dir))
                {
                    var ext = Path.GetExtension(filePath);
                    if (!validExtensions.Contains(ext)) continue;

                    // If not already in list, extract metadata from file name
                    if (!_downloadedSongs.Any(s => string.Equals(s.LocalFilePath, filePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        var rawName = Path.GetFileNameWithoutExtension(filePath);
                        string title = rawName;
                        string artist = "Offline Audio";
                        string videoId = "offline_" + Math.Abs(rawName.GetHashCode());

                        // Format: {Artist} - {Title}_{VideoId}
                        int underIdx = rawName.LastIndexOf('_');
                        if (underIdx > 0 && underIdx < rawName.Length - 1)
                        {
                            videoId = rawName.Substring(underIdx + 1);
                            rawName = rawName.Substring(0, underIdx);
                        }

                        int dashIdx = rawName.IndexOf(" - ", StringComparison.Ordinal);
                        if (dashIdx > 0)
                        {
                            artist = rawName.Substring(0, dashIdx).Trim();
                            title = rawName.Substring(dashIdx + 3).Trim();
                        }

                        var fileInfo = new FileInfo(filePath);
                        var sizeMb = fileInfo.Length / (1024.0 * 1024.0);

                        _downloadedSongs.Add(new Song
                        {
                            VideoId = videoId,
                            Title = title,
                            Artist = artist,
                            Album = "Downloads",
                            LocalFilePath = filePath,
                            FileSizeFormatted = $"{sizeMb:F1} MB · {ext.TrimStart('.').ToUpperInvariant()}",
                            ThumbnailUrl = "ms-appx:///Assets/StoreLogo.png"
                        });
                    }
                }
            }

            await SaveManifestAsync();
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to load downloads manifest", ex);
        }
    }

    private async Task SaveManifestAsync()
    {
        try
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            var file = await localFolder.CreateFileAsync(ManifestFileName, CreationCollisionOption.ReplaceExisting);
            var json = JsonSerializer.Serialize(_downloadedSongs, new JsonSerializerOptions { WriteIndented = true });
            await FileIO.WriteTextAsync(file, json);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to save downloads manifest", ex);
        }
    }

    public async Task<IReadOnlyList<Song>> GetDownloadedSongsAsync()
    {
        await EnsureLoadedAsync();
        return _downloadedSongs.ToList();
    }

    public bool IsSongDownloaded(string videoId)
    {
        return _downloadedSongs.Any(s => s.VideoId == videoId && !string.IsNullOrEmpty(s.LocalFilePath) && File.Exists(s.LocalFilePath));
    }

    public async Task<string> GetDownloadsFolderPathAsync()
    {
        var localFolder = ApplicationData.Current.LocalFolder;
        var folder = await localFolder.CreateFolderAsync("Downloads", CreationCollisionOption.OpenIfExists);
        return folder.Path;
    }

    public async Task<bool> DownloadSongAsync(Song song, Action<double>? progressCallback = null)
    {
        if (string.IsNullOrEmpty(song.VideoId)) return false;

        await EnsureLoadedAsync();

        if (IsSongDownloaded(song.VideoId))
        {
            AppLogger.Log($"Song {song.Title} already downloaded.");
            return true;
        }

        try
        {
            AppLogger.Log($"Starting download for: {song.Title} ({song.VideoId})");

            // 1. If ID is a standard 11-char YouTube ID (and not a Qobuz / custom ID)
            bool isYouTubeId = song.VideoId.Length == 11 && song.VideoId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
            if (isYouTubeId)
            {
                try
                {
                    var manifest = await _youtube.Videos.Streams.GetManifestAsync(song.VideoId);
                    var audioStream = manifest.GetAudioOnlyStreams()
                        .OrderByDescending(s => s.Bitrate)
                        .FirstOrDefault();

                    if (audioStream != null)
                    {
                        var downloadsFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Downloads", CreationCollisionOption.OpenIfExists);
                        var safeTitle = MakeSafeFileName($"{song.Artist} - {song.Title}");
                        var ext = audioStream.Container.Name switch
                        {
                            "mp4" => "m4a",
                            "webm" => "opus",
                            _ => audioStream.Container.Name
                        };
                        var fileName = $"{safeTitle}_{song.VideoId}.{ext}";
                        var file = await downloadsFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                        var progress = new Progress<double>(p => progressCallback?.Invoke(p));
                        await _youtube.Videos.Streams.DownloadAsync(audioStream, file.Path, progress);

                        var fileInfo = new FileInfo(file.Path);
                        var sizeMb = fileInfo.Length / (1024.0 * 1024.0);

                        song.LocalFilePath = file.Path;
                        song.FileSizeFormatted = $"{sizeMb:F1} MB · {ext.ToUpperInvariant()}";

                        _downloadedSongs.RemoveAll(s => s.VideoId == song.VideoId);
                        _downloadedSongs.Insert(0, song);

                        await SaveManifestAsync();
                        DownloadedSongsChanged?.Invoke(this, EventArgs.Empty);
                        AppLogger.Log($"Successfully downloaded {song.Title} via YoutubeExplode to {file.Path}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError($"YoutubeExplode failed for {song.Title}, attempting stream URL fallback", ex);
                }
            }

            // 2. Direct HTTP Stream Download (for Qobuz / BravePipe / YouTube stream URL fallback)
            string? streamUrl = song.StreamUrl;
            if (string.IsNullOrEmpty(streamUrl))
            {
                var musicService = App.Services.GetService(typeof(IYouTubeMusicService)) as IYouTubeMusicService;
                if (musicService != null)
                {
                    streamUrl = await musicService.GetAudioStreamUrlAsync(song.VideoId);
                }
            }

            if (!string.IsNullOrEmpty(streamUrl))
            {
                return await DownloadViaHttpAsync(song, streamUrl, progressCallback);
            }

            AppLogger.Log($"No downloadable stream could be resolved for {song.Title}");
            return false;
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"Download failed for {song.Title}", ex);
            return false;
        }
    }

    private async Task<bool> DownloadViaHttpAsync(Song song, string streamUrl, Action<double>? progressCallback)
    {
        try
        {
            var downloadsFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Downloads", CreationCollisionOption.OpenIfExists);
            var safeTitle = MakeSafeFileName($"{song.Artist} - {song.Title}");
            var safeId = MakeSafeFileName(song.VideoId);
            if (safeId.Length > 20) safeId = safeId.Substring(0, 20);

            var tempFileName = $"{safeTitle}_{safeId}.tmp";
            var tempFile = await downloadsFolder.CreateFileAsync(tempFileName, CreationCollisionOption.ReplaceExisting);

            using var response = await _httpClient.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempFile.Path, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true);

            var buffer = new byte[16384];
            long totalRead = 0;
            int read;
            byte[] firstBytes = new byte[16];
            int firstBytesRead = 0;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (firstBytesRead < 16)
                {
                    int toCopy = Math.Min(read, 16 - firstBytesRead);
                    Array.Copy(buffer, 0, firstBytes, firstBytesRead, toCopy);
                    firstBytesRead += toCopy;
                }

                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (totalBytes > 0 && progressCallback != null)
                {
                    progressCallback((double)totalRead / totalBytes);
                }
            }
            fileStream.Close();

            // Detect true audio format from Magic Bytes & Content-Type
            string ext = "m4a";
            string codecName = "AAC (256 kbps)";

            bool isFlacMagic = firstBytesRead >= 4 && firstBytes[0] == 0x66 && firstBytes[1] == 0x4C && firstBytes[2] == 0x61 && firstBytes[3] == 0x43;
            bool isMp4Magic = firstBytesRead >= 8 && firstBytes[4] == 0x66 && firstBytes[5] == 0x74 && firstBytes[6] == 0x79 && firstBytes[7] == 0x70;
            bool isOggMagic = firstBytesRead >= 4 && firstBytes[0] == 0x4F && firstBytes[1] == 0x67 && firstBytes[2] == 0x67 && firstBytes[3] == 0x53;

            if (isFlacMagic || contentType.Contains("flac") || streamUrl.Contains(".flac", StringComparison.OrdinalIgnoreCase) || song.VideoId.StartsWith("qobuz_"))
            {
                ext = "flac";
                codecName = "FLAC (Lossless Hi-Res)";
            }
            else if (isOggMagic || contentType.Contains("ogg") || contentType.Contains("opus") || streamUrl.Contains(".opus", StringComparison.OrdinalIgnoreCase))
            {
                ext = "opus";
                codecName = "Opus (Hi-Fi)";
            }
            else if (isMp4Magic || contentType.Contains("mp4") || contentType.Contains("aac") || contentType.Contains("m4a") || streamUrl.Contains(".m4a", StringComparison.OrdinalIgnoreCase))
            {
                ext = "m4a";
                codecName = "AAC (256 kbps)";
            }
            else
            {
                ext = "m4a";
                codecName = "AAC (256 kbps)";
            }

            var finalFileName = $"{safeTitle}_{safeId}.{ext}";
            var finalPath = Path.Combine(downloadsFolder.Path, finalFileName);
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempFile.Path, finalPath);

            var fileInfo = new FileInfo(finalPath);
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);

            song.LocalFilePath = finalPath;
            song.FileSizeFormatted = $"{sizeMb:F1} MB · {ext.ToUpperInvariant()}";
            song.AudioQuality = $"{ext.ToUpperInvariant()} · {codecName}";
            song.AudioCodec = ext.ToUpperInvariant();
            song.AudioSource = ext == "flac" ? "Qobuz Lossless" : "High Quality Stream";

            _downloadedSongs.RemoveAll(s => s.VideoId == song.VideoId);
            _downloadedSongs.Insert(0, song);

            await SaveManifestAsync();
            DownloadedSongsChanged?.Invoke(this, EventArgs.Empty);

            AppLogger.Log($"Successfully downloaded {song.Title} via HTTP to {finalPath} ({song.FileSizeFormatted})");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"DownloadViaHttpAsync failed for {song.Title}", ex);
            return false;
        }
    }

    public async Task<bool> ImportLocalSongAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

        await EnsureLoadedAsync();

        try
        {
            var fileInfo = new FileInfo(filePath);
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
            var ext = fileInfo.Extension.TrimStart('.').ToUpperInvariant();

            // Extract title and artist from filename (e.g. "Artist - Title.mp3")
            var rawName = Path.GetFileNameWithoutExtension(filePath);
            var parts = rawName.Split(new[] { " - " }, 2, StringSplitOptions.RemoveEmptyEntries);
            string artist = parts.Length > 1 ? parts[0].Trim() : "Local Track";
            string title = parts.Length > 1 ? parts[1].Trim() : rawName;

            var localSong = new Song
            {
                VideoId = "local_" + Guid.NewGuid().ToString("N"),
                Title = title,
                Artist = artist,
                LocalFilePath = filePath,
                FileSizeFormatted = $"{sizeMb:F1} MB · {ext}",
                AudioQuality = $"Local {ext}",
                AudioCodec = ext,
                AudioSource = "Local PC Storage"
            };

            _downloadedSongs.RemoveAll(s => s.LocalFilePath == filePath);
            _downloadedSongs.Insert(0, localSong);

            await SaveManifestAsync();
            DownloadedSongsChanged?.Invoke(this, EventArgs.Empty);

            AppLogger.Log($"Imported local track: {title} from {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"ImportLocalSongAsync failed for {filePath}", ex);
            return false;
        }
    }

    public async Task<bool> DeleteDownloadedSongAsync(string videoId)
    {
        await EnsureLoadedAsync();
        var existing = _downloadedSongs.FirstOrDefault(s => s.VideoId == videoId);
        if (existing != null)
        {
            try
            {
                if (!string.IsNullOrEmpty(existing.LocalFilePath) && File.Exists(existing.LocalFilePath))
                {
                    File.Delete(existing.LocalFilePath);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Failed to delete file {existing.LocalFilePath}", ex);
            }

            _downloadedSongs.Remove(existing);
            await SaveManifestAsync();
            DownloadedSongsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }

    private static string MakeSafeFileName(string input)
    {
        var safe = string.Join("_", (input ?? "").Split(Path.GetInvalidFileNameChars())).Trim();
        return safe.Length > 60 ? safe.Substring(0, 60) : safe;
    }
}
