using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.Media.Playback;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class PlayerBarViewModel : ObservableObject, IDisposable
{
    private readonly IMediaPlaybackService _playbackService;
    private readonly INavigationService? _navService;

    [ObservableProperty]
    private string _title = "Not Playing";

    [ObservableProperty]
    private string _artist = "Choose a song to start listening";

    [ObservableProperty]
    private string _thumbnailUrl = string.Empty;

    [ObservableProperty]
    private string _audioQualityBadge = "256 kbps · AAC · Hi-Res";

    [ObservableProperty]
    private string _audioCodec = "AAC";

    [ObservableProperty]
    private string _audioSource = "BravePipe / Qobuz";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private TimeSpan _position;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.FromSeconds(1);

    [ObservableProperty]
    private double _volume = 0.8;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _hasSong;

    [ObservableProperty]
    private double _playbackRate = 1.0;

    [ObservableProperty]
    private bool _isShuffle;

    [ObservableProperty]
    private int _repeatMode; // 0=Off, 1=All, 2=One

    [ObservableProperty]
    private bool _isDrawerOpen;

    [ObservableProperty]
    private int _selectedDrawerTab; // 0 = Up Next, 1 = Lyrics, 2 = History

    public IMediaPlaybackService PlaybackService => _playbackService;

    public PlayerBarViewModel(IMediaPlaybackService playbackService, INavigationService? navService = null)
    {
        _playbackService = playbackService;
        _navService = navService;

        _playbackService.SongChanged += OnSongChanged;
        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
        _playbackService.PositionChanged += OnPositionChanged;
        _playbackService.QueueChanged += OnQueueChanged;

        Volume = _playbackService.Volume;
        PlaybackRate = _playbackService.PlaybackRate;
    }

    private void OnSongChanged(object? sender, Song song)
    {
        Title = song.Title;
        Artist = song.Artist;
        ThumbnailUrl = song.ThumbnailUrl;
        AudioQualityBadge = !string.IsNullOrEmpty(song.AudioQuality) ? song.AudioQuality : "256 kbps · AAC · Hi-Res";
        AudioCodec = !string.IsNullOrEmpty(song.AudioCodec) ? song.AudioCodec : "AAC";
        AudioSource = !string.IsNullOrEmpty(song.AudioSource) ? song.AudioSource : "BravePipe / Qobuz";
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

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PlaybackService));
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        _playbackService.TogglePlayPause();
    }

    [RelayCommand]
    private async Task NextTrackAsync()
    {
        await _playbackService.PlayNextAsync();
    }

    [RelayCommand]
    private async Task PreviousTrackAsync()
    {
        await _playbackService.PlayPreviousAsync();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        _playbackService.IsMuted = IsMuted;
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        IsShuffle = !IsShuffle;
        _playbackService.IsShuffleEnabled = IsShuffle;
    }

    [RelayCommand]
    private void ToggleRepeat()
    {
        RepeatMode = (RepeatMode + 1) % 3;
        _playbackService.RepeatMode = RepeatMode;
    }

    [RelayCommand]
    private void SetSpeed(string speedStr)
    {
        if (double.TryParse(speedStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate))
        {
            PlaybackRate = rate;
            _playbackService.PlaybackRate = rate;
        }
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

    [RelayCommand]
    private void ToggleLyrics()
    {
        SelectedDrawerTab = 1;
        IsDrawerOpen = !IsDrawerOpen;
    }

    [RelayCommand]
    private void ToggleQueue()
    {
        SelectedDrawerTab = 0;
        IsDrawerOpen = !IsDrawerOpen;
    }

    public void Dispose()
    {
        _playbackService.SongChanged -= OnSongChanged;
        _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _playbackService.PositionChanged -= OnPositionChanged;
        _playbackService.QueueChanged -= OnQueueChanged;
    }
}
