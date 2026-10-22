using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public interface IDownloadService
{
    event EventHandler? DownloadedSongsChanged;
    Task<IReadOnlyList<Song>> GetDownloadedSongsAsync();
    Task<bool> DownloadSongAsync(Song song, Action<double>? progressCallback = null);
    Task<bool> ImportLocalSongAsync(string filePath);
    Task<bool> DeleteDownloadedSongAsync(string videoId);
    Task<string> GetDownloadsFolderPathAsync();
    bool IsSongDownloaded(string videoId);
}
