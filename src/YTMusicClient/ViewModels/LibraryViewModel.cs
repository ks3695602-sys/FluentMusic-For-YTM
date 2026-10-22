using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IMediaPlaybackService _playbackService;
    private readonly IYouTubeMusicService _musicService;
    private readonly IDownloadService? _downloadService;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isGoogleConnected;

    [ObservableProperty]
    private string _googleUserName = "Google User";

    public ObservableCollection<Song> LikedSongs { get; } = new();
    public ObservableCollection<Playlist> Playlists { get; } = new();

    public LibraryViewModel(
        IMediaPlaybackService playbackService,
        IYouTubeMusicService musicService,
        IDownloadService? downloadService = null)
    {
        _playbackService = playbackService;
        _musicService = musicService;
        _downloadService = downloadService;

        CheckGoogleStatus();
        _ = LoadLibraryAsync();
    }

    public void CheckGoogleStatus()
    {
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            IsGoogleConnected = localSettings.Values["IsGoogleSignedIn"] is bool b && b;
            if (IsGoogleConnected)
            {
                GoogleUserName = localSettings.Values["UserDisplayName"] as string ?? "Google Account";
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task LoadLibraryAsync()
    {
        CheckGoogleStatus();

        if (LikedSongs.Count > 0) return;

        IsLoading = true;
        try
        {
            var songs = await _musicService.GetLikedSongsAsync();
            LikedSongs.Clear();
            foreach (var s in songs)
            {
                LikedSongs.Add(s);
            }
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SyncWithGoogleAsync()
    {
        IsLoading = true;
        try
        {
            CheckGoogleStatus();
            var songs = await _musicService.GetLikedSongsAsync();
            LikedSongs.Clear();
            foreach (var s in songs)
            {
                LikedSongs.Add(s);
            }
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlaySongAsync(Song? song)
    {
        if (song == null) return;
        await _playbackService.PlayAsync(song);
    }

    [RelayCommand]
    private void AddToLiked()
    {
        var current = _playbackService.CurrentSong;
        if (current != null && !LikedSongs.Any(s => s.VideoId == current.VideoId))
        {
            LikedSongs.Insert(0, current);
        }
    }
}
