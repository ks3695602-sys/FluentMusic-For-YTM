using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.Media.Playback;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class PlayerBarViewModel : ObservableObject, IDisposable
{
    private readonly IMediaPlaybackService _playbackService;

    [ObservableProperty]
    private string _title = "Not Playing";

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private string _thumbnailUrl = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private TimeSpan _position;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.FromSeconds(1); // Avoid div by zero

    [ObservableProperty]
    private double _volume = 0.8;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _hasSong;

    public PlayerBarViewModel(IMediaPlaybackService playbackService)
    {
        _playbackService = playbackService;
        _playbackService.SongChanged += OnSongChanged;
        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
        _playbackService.PositionChanged += OnPositionChanged;
        Volume = _playbackService.Volume;
    }

    private void OnSongChanged(object? sender, Song song)
    {
        Title = song.Title;
        Artist = song.Artist;
        ThumbnailUrl = song.ThumbnailUrl;
        HasSong = true;
    }

    private void OnPlaybackStateChanged(object? sender, MediaPlaybackState state)
    {
        IsPlaying = state == MediaPlaybackState.Playing;
        if (state == MediaPlaybackState.Playing || state == MediaPlaybackState.Paused)
        {
            Duration = _playbackService.Duration;
        }
    }

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        Position = position;
        if (Duration.TotalSeconds > 0)
        {
            ProgressPercent = (position.TotalSeconds / Duration.TotalSeconds) * 100.0;
        }
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        _playbackService.TogglePlayPause();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        _playbackService.IsMuted = IsMuted;
    }

    partial void OnVolumeChanged(double value)
    {
        _playbackService.Volume = value;
    }

    [RelayCommand]
    private void SeekTo(double percentage)
    {
        var newPosition = TimeSpan.FromSeconds(Duration.TotalSeconds * (percentage / 100.0));
        _playbackService.Seek(newPosition);
    }

    public void Dispose()
    {
        _playbackService.SongChanged -= OnSongChanged;
        _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _playbackService.PositionChanged -= OnPositionChanged;
    }
}
