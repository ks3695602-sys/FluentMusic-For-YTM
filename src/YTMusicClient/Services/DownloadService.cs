using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private readonly List<Song> _downloadedSongs = new();
    private const string ManifestFileName = "downloads_manifest.json";
    private bool _isLoaded;

    public event EventHandler? DownloadedSongsChanged;

    public DownloadService()
    {
        _youtube = new YoutubeClient();
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

            var manifest = await _youtube.Videos.Streams.GetManifestAsync(song.VideoId);
            var audioStream = manifest.GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (audioStream == null)
            {
                AppLogger.Log($"No audio stream available for {song.Title}");
                return false;
            }

            var localFolder = ApplicationData.Current.LocalFolder;
            var downloadsFolder = await localFolder.CreateFolderAsync("Downloads", CreationCollisionOption.OpenIfExists);

            var safeTitle = string.Join("_", $"{song.Artist} - {song.Title}".Split(Path.GetInvalidFileNameChars())).Trim();
            if (safeTitle.Length > 80) safeTitle = safeTitle.Substring(0, 80);
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
            AppLogger.Log($"Successfully downloaded {song.Title} to {file.Path} ({song.FileSizeFormatted})");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"Download failed for {song.Title}", ex);
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
}
