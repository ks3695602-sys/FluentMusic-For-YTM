using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;
    private readonly IMediaPlaybackService _playbackService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _storageUsedFormatted = "0 MB";

    [ObservableProperty]
    private int _songCount;

    [ObservableProperty]
    private bool _hasDownloads;

    public ObservableCollection<Song> DownloadedSongs { get; } = new();

    public DownloadsViewModel(IDownloadService downloadService, IMediaPlaybackService playbackService)
    {
        _downloadService = downloadService;
        _playbackService = playbackService;

        _downloadService.DownloadedSongsChanged += (s, e) => _ = LoadDownloadsAsync();
        _ = LoadDownloadsAsync();
    }

    [RelayCommand]
    public async Task LoadDownloadsAsync()
    {
        IsLoading = true;
        try
        {
            var songs = await _downloadService.GetDownloadedSongsAsync();
            DownloadedSongs.Clear();

            double totalBytes = 0;
            foreach (var s in songs)
            {
                DownloadedSongs.Add(s);
                if (!string.IsNullOrEmpty(s.LocalFilePath) && File.Exists(s.LocalFilePath))
                {
                    totalBytes += new FileInfo(s.LocalFilePath).Length;
                }
            }

            SongCount = DownloadedSongs.Count;
            HasDownloads = SongCount > 0;
            StorageUsedFormatted = $"{totalBytes / (1024.0 * 1024.0):F1} MB";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PlaySongAsync(Song? song)
    {
        if (song == null) return;
        await _playbackService.PlayAsync(song);
    }

    [RelayCommand]
    public async Task PlayAllAsync()
    {
        if (DownloadedSongs.Count == 0) return;

        _playbackService.ClearQueue();
        for (int i = 1; i < DownloadedSongs.Count; i++)
        {
            _playbackService.Enqueue(DownloadedSongs[i]);
        }

        await _playbackService.PlayAsync(DownloadedSongs[0]);
    }

    [RelayCommand]
    public async Task DeleteSongAsync(Song? song)
    {
        if (song == null) return;
        await _downloadService.DeleteDownloadedSongAsync(song.VideoId);
        await LoadDownloadsAsync();
    }

    [RelayCommand]
    public async Task ImportLocalFilesAsync(System.Collections.Generic.IEnumerable<string> filePaths)
    {
        bool anyAdded = false;
        foreach (var file in filePaths)
        {
            if (await _downloadService.ImportLocalSongAsync(file))
            {
                anyAdded = true;
            }
        }
        if (anyAdded)
        {
            await LoadDownloadsAsync();
        }
    }

    [RelayCommand]
    public async Task OpenFolderAsync()
    {
        var path = await _downloadService.GetDownloadsFolderPathAsync();
        if (Directory.Exists(path))
        {
            await Windows.System.Launcher.LaunchFolderPathAsync(path);
        }
    }
}
