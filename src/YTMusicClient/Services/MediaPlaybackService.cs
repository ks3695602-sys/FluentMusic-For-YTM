using System.Diagnostics;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public class MediaPlaybackService : IMediaPlaybackService, IDisposable
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly SystemMediaTransportControls _smtc;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private System.Threading.Timer? _positionTimer;

    public MediaPlaybackState PlaybackState => _mediaPlayer.PlaybackSession.PlaybackState;
    public TimeSpan Position => _mediaPlayer.PlaybackSession.Position;
    public TimeSpan Duration => _mediaPlayer.PlaybackSession.NaturalDuration;
    public bool IsPlaying => PlaybackState == MediaPlaybackState.Playing;

    public double Volume
    {
        get => _mediaPlayer.Volume;
        set => _mediaPlayer.Volume = Math.Clamp(value, 0.0, 1.0);
    }

    public bool IsMuted
    {
        get => _mediaPlayer.IsMuted;
        set => _mediaPlayer.IsMuted = value;
    }

    public Song? CurrentSong { get; private set; }

    public event EventHandler<Song>? SongChanged;
    public event EventHandler<MediaPlaybackState>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;

    public MediaPlaybackService()
    {
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;
        _mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _mediaPlayer.MediaEnded += OnMediaEnded;
        _mediaPlayer.MediaOpened += OnMediaOpened;
        _mediaPlayer.MediaFailed += OnMediaFailed;

        // Configure SMTC
        _smtc = _mediaPlayer.SystemMediaTransportControls;
        _smtc.IsEnabled = true;
        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
        _smtc.IsStopEnabled = true;
        _smtc.IsNextEnabled = true;
        _smtc.IsPreviousEnabled = true;
        _smtc.ButtonPressed += OnSmtcButtonPressed;

        // Position timer - fires every 250ms to update position
        _positionTimer = new System.Threading.Timer(OnPositionTimerTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(250));
    }

    public async Task PlayAsync(Song song)
    {
        try
        {
            CurrentSong = song;

            if (string.IsNullOrEmpty(song.StreamUrl))
            {
                Debug.WriteLine("No stream URL available");
                return;
            }

            _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(song.StreamUrl));
            _mediaPlayer.Play();

            // Update SMTC display
            await UpdateSmtcDisplayAsync(song);

            _dispatcherQueue.TryEnqueue(() =>
            {
                SongChanged?.Invoke(this, song);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Play failed: {ex.Message}");
        }
    }

    public void Pause() => _mediaPlayer.Pause();

    public void Resume() => _mediaPlayer.Play();

    public void TogglePlayPause()
    {
        if (IsPlaying)
            Pause();
        else
            Resume();
    }

    public void Stop()
    {
        _mediaPlayer.Pause();
        _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
    }

    public void Seek(TimeSpan position)
    {
        if (position >= TimeSpan.Zero && position <= Duration)
            _mediaPlayer.PlaybackSession.Position = position;
    }

    private async Task UpdateSmtcDisplayAsync(Song song)
    {
        try
        {
            var updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = song.Title;
            updater.MusicProperties.Artist = song.Artist;
            updater.MusicProperties.AlbumTitle = song.Album;

            if (!string.IsNullOrEmpty(song.ThumbnailUrl))
            {
                try
                {
                    updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(song.ThumbnailUrl));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SMTC thumbnail set failed: {ex.Message}");
                }
            }

            updater.Update();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SMTC update failed: {ex.Message}");
        }
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            PlaybackStateChanged?.Invoke(this, _mediaPlayer.PlaybackSession.PlaybackState);
        });
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine($"MediaPlayer Failed: {args.Error} - {args.ErrorMessage} (HResult: 0x{args.ExtendedErrorCode:X8})");
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            PlaybackStateChanged?.Invoke(this, sender.PlaybackState);
        });
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            PlaybackStateChanged?.Invoke(this, MediaPlaybackState.None);
        });
    }

    private void OnSmtcButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    Resume();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    Pause();
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    Stop();
                    break;
            }
        });
    }

    private void OnPositionTimerTick(object? state)
    {
        try
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (IsPlaying)
                {
                    PositionChanged?.Invoke(this, Position);
                }
            });
        }
        catch { /* Timer may fire after disposal */ }
    }

    public void Dispose()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
        _smtc.ButtonPressed -= OnSmtcButtonPressed;
        _mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _mediaPlayer.MediaEnded -= OnMediaEnded;
        _mediaPlayer.MediaOpened -= OnMediaOpened;
        _mediaPlayer.MediaFailed -= OnMediaFailed;
        _mediaPlayer.Dispose();
    }
}
