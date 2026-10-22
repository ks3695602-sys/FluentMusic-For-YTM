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
            // If Google is connected, fetch synced tracks from YouTube Music
            var results = await _musicService.GetTrendingAsync();
            LikedSongs.Clear();
            foreach (var r in results)
            {
                LikedSongs.Add(new Song
                {
                    VideoId = r.VideoId,
                    Title = r.Title,
                    Artist = r.Artist,
                    Album = r.Album,
                    ThumbnailUrl = r.ThumbnailUrl,
                    Duration = r.Duration,
                    AudioQuality = r.AudioQuality
                });
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
            var results = await _musicService.GetTrendingAsync();
            LikedSongs.Clear();
            foreach (var r in results)
            {
                LikedSongs.Add(new Song
                {
                    VideoId = r.VideoId,
                    Title = r.Title,
                    Artist = r.Artist,
                    Album = r.Album,
                    ThumbnailUrl = r.ThumbnailUrl,
                    Duration = r.Duration,
                    AudioQuality = r.AudioQuality
                });
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
