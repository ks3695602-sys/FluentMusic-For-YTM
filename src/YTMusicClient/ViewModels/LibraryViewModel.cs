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
    public ObservableCollection<Playlist> FilteredPlaylists { get; } = new();
    public ObservableCollection<Song> PlaylistTracks { get; } = new();

    [ObservableProperty]
    private Playlist? _selectedPlaylist;

    [ObservableProperty]
    private bool _isPlaylistDetailOpen;

    [ObservableProperty]
    private string _playlistSearchQuery = string.Empty;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0 = Playlists, 1 = Liked Songs

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

    partial void OnPlaylistSearchQueryChanged(string value)
    {
        ApplyPlaylistFilter();
    }

    private void ApplyPlaylistFilter()
    {
        FilteredPlaylists.Clear();
        var q = PlaylistSearchQuery?.Trim().ToLowerInvariant() ?? "";
        foreach (var p in Playlists)
        {
            if (string.IsNullOrEmpty(q) || p.Title.ToLowerInvariant().Contains(q) || p.Description.ToLowerInvariant().Contains(q))
            {
                FilteredPlaylists.Add(p);
            }
        }
    }

    [RelayCommand]
    public async Task LoadLibraryAsync()
    {
        CheckGoogleStatus();

        if (Playlists.Count > 0 && LikedSongs.Count > 0) return;

        IsLoading = true;
        try
        {
            // 1. Load User Playlists
            var playlists = await _musicService.GetLibraryPlaylistsAsync();
            Playlists.Clear();
            foreach (var p in playlists)
            {
                Playlists.Add(p);
            }
            ApplyPlaylistFilter();

            // 2. Load Liked Songs
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
            var playlists = await _musicService.GetLibraryPlaylistsAsync();
            Playlists.Clear();
            foreach (var p in playlists)
            {
                Playlists.Add(p);
            }
            ApplyPlaylistFilter();

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
    public async Task OpenPlaylistAsync(Playlist? playlist)
    {
        if (playlist == null) return;
        SelectedPlaylist = playlist;
        IsPlaylistDetailOpen = true;

        PlaylistTracks.Clear();
        IsLoading = true;
        try
        {
            var tracks = await _musicService.GetPlaylistTracksAsync(playlist.Id);
            foreach (var t in tracks)
            {
                PlaylistTracks.Add(t);
            }
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ClosePlaylistDetail()
    {
        IsPlaylistDetailOpen = false;
        SelectedPlaylist = null;
    }

    [RelayCommand]
    public async Task PlayAllPlaylistAsync()
    {
        if (PlaylistTracks.Count == 0) return;
        var first = PlaylistTracks[0];
        await _playbackService.PlayAsync(first);

        foreach (var t in PlaylistTracks.Skip(1))
        {
            _playbackService.UpNext.Add(t);
        }
    }

    [RelayCommand]
    public async Task ShufflePlaylistAsync()
    {
        if (PlaylistTracks.Count == 0) return;
        var rnd = new Random();
        var shuffled = PlaylistTracks.OrderBy(x => rnd.Next()).ToList();
        await _playbackService.PlayAsync(shuffled[0]);

        foreach (var t in shuffled.Skip(1))
        {
            _playbackService.UpNext.Add(t);
        }
    }

    [RelayCommand]
    public async Task PlaySongAsync(Song? song)
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
