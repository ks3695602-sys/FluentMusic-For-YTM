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

            string ext = "mp3";
            if (streamUrl.Contains(".flac", StringComparison.OrdinalIgnoreCase)) ext = "flac";
            else if (streamUrl.Contains(".m4a", StringComparison.OrdinalIgnoreCase)) ext = "m4a";
            else if (streamUrl.Contains(".opus", StringComparison.OrdinalIgnoreCase)) ext = "opus";

            var safeId = MakeSafeFileName(song.VideoId);
            if (safeId.Length > 20) safeId = safeId.Substring(0, 20);

            var fileName = $"{safeTitle}_{safeId}.{ext}";
            var file = await downloadsFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            using var response = await _httpClient.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(file.Path, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true);

            var buffer = new byte[16384];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (totalBytes > 0 && progressCallback != null)
                {
                    progressCallback((double)totalRead / totalBytes);
                }
            }

            var fileInfo = new FileInfo(file.Path);
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);

            song.LocalFilePath = file.Path;
            song.FileSizeFormatted = $"{sizeMb:F1} MB · {ext.ToUpperInvariant()}";

            _downloadedSongs.RemoveAll(s => s.VideoId == song.VideoId);
            _downloadedSongs.Insert(0, song);

            await SaveManifestAsync();
            DownloadedSongsChanged?.Invoke(this, EventArgs.Empty);

            AppLogger.Log($"Successfully downloaded {song.Title} via HTTP to {file.Path} ({song.FileSizeFormatted})");
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
