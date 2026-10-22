using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using YTMusicClient.Helpers;
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

    public double PlaybackRate
    {
        get => _mediaPlayer.PlaybackSession.PlaybackRate;
        set
        {
            var clamped = Math.Clamp(value, 0.25, 3.0);
            _mediaPlayer.PlaybackSession.PlaybackRate = clamped;
            AppLogger.Log($"PlaybackRate set to {clamped}x");
        }
    }

    public bool IsShuffleEnabled { get; set; }
    public int RepeatMode { get; set; } // 0 = Off, 1 = Repeat All, 2 = Repeat One

    public ObservableCollection<Song> UpNext { get; } = new();
    public ObservableCollection<Song> History { get; } = new();

    public Song? CurrentSong { get; private set; }

    public event EventHandler<Song>? SongChanged;
    public event EventHandler<MediaPlaybackState>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? QueueChanged;

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

    public void SetCurrentSong(Song song)
    {
        CurrentSong = song;
        AppLogger.Log($"SetCurrentSong: {song.Title} by {song.Artist}");
        _dispatcherQueue.TryEnqueue(() =>
        {
            SongChanged?.Invoke(this, song);
        });
    }

    public async Task PlayAsync(Song song)
    {
        try
        {
            SetCurrentSong(song);

            // Check if playing offline downloaded track
            if (!string.IsNullOrEmpty(song.LocalFilePath) && System.IO.File.Exists(song.LocalFilePath))
            {
                AppLogger.Log($"PlayAsync: Playing offline local file: {song.LocalFilePath}");
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(song.LocalFilePath);
                _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
                _mediaPlayer.Play();
                await UpdateSmtcDisplayAsync(song);
                return;
            }

            if (string.IsNullOrEmpty(song.StreamUrl))
            {
                // If stream URL is not preloaded, fetch via YouTubeMusicService
                try
                {
                    var musicService = App.Services.GetService(typeof(IYouTubeMusicService)) as IYouTubeMusicService;
                    if (musicService != null)
                    {
                        song.StreamUrl = await musicService.GetAudioStreamUrlAsync(song.VideoId);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Failed to resolve stream URL in PlayAsync", ex);
                }
            }

            if (string.IsNullOrEmpty(song.StreamUrl))
            {
                AppLogger.Log("PlayAsync: No stream URL available for " + song.Title);
                return;
            }

            AppLogger.Log($"PlayAsync: Setting MediaPlayer source to {song.StreamUrl.Substring(0, Math.Min(60, song.StreamUrl.Length))}...");
            _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(song.StreamUrl));
            _mediaPlayer.Play();
            AppLogger.Log("PlayAsync: MediaPlayer.Play() invoked");

            // Update SMTC display
            await UpdateSmtcDisplayAsync(song);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("PlayAsync failed", ex);
        }
    }

    public void Pause()
    {
        AppLogger.Log("Pause invoked");
        _mediaPlayer.Pause();
    }

    public void Resume()
    {
        AppLogger.Log("Resume invoked");
        _mediaPlayer.Play();
    }

    public void TogglePlayPause()
    {
        if (IsPlaying)
            Pause();
        else
            Resume();
    }

    public void Stop()
    {
        AppLogger.Log("Stop invoked");
        _mediaPlayer.Pause();
        _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
    }

    public void Seek(TimeSpan position)
    {
        AppLogger.Log($"Seek to: {position}");
        _mediaPlayer.PlaybackSession.Position = position;
    }

    public async Task PlayNextAsync()
    {
        if (UpNext.Count > 0)
        {
            if (CurrentSong != null)
            {
                History.Add(CurrentSong);
            }

            var nextSong = UpNext[0];
            UpNext.RemoveAt(0);

            _dispatcherQueue.TryEnqueue(() => QueueChanged?.Invoke(this, EventArgs.Empty));
            await PlayAsync(nextSong);
        }
        else if (RepeatMode == 1 && History.Count > 0)
        {
            // Repeat All: move history to queue and restart
            foreach (var h in History)
            {
                UpNext.Add(h);
            }
            History.Clear();
            if (CurrentSong != null)
            {
                UpNext.Add(CurrentSong);
            }
            if (UpNext.Count > 0)
            {
                var first = UpNext[0];
                UpNext.RemoveAt(0);
                _dispatcherQueue.TryEnqueue(() => QueueChanged?.Invoke(this, EventArgs.Empty));
                await PlayAsync(first);
            }
        }
        else
        {
            AppLogger.Log("PlayNextAsync: Queue is empty");
        }
    }

    public async Task PlayPreviousAsync()
    {
        if (Position > TimeSpan.FromSeconds(3))
        {
            Seek(TimeSpan.Zero);
            return;
        }

        if (History.Count > 0)
        {
            var prevSong = History.Last();
            History.RemoveAt(History.Count - 1);

            if (CurrentSong != null)
            {
                UpNext.Insert(0, CurrentSong);
            }

            _dispatcherQueue.TryEnqueue(() => QueueChanged?.Invoke(this, EventArgs.Empty));
            await PlayAsync(prevSong);
        }
        else
        {
            Seek(TimeSpan.Zero);
        }
    }

    public void Enqueue(Song song)
    {
        UpNext.Add(song);
        _dispatcherQueue.TryEnqueue(() => QueueChanged?.Invoke(this, EventArgs.Empty));
        AppLogger.Log($"Enqueue: Added '{song.Title}' to queue. Total: {UpNext.Count}");
    }

    public void EnqueueNext(Song song)
    {
        UpNext.Insert(0, song);
        _dispatcherQueue.TryEnqueue(() => QueueChanged?.Invoke(this, EventArgs.Empty));
        AppLogger.Log($"EnqueueNext: Inserted '{song.Title}' as next track. Total: {UpNext.Count}");
    }

    public void RemoveFromQueue(Song song)
    {
        UpNext.Remove(song);
        _dispatcherQueue.TryEnqueue(() => QueueChanged?.Invoke(this, EventArgs.Empty));
    }

    public void ClearQueue()
    {
        UpNext.Clear();
        _dispatcherQueue.TryEnqueue(() => QueueChanged?.Invoke(this, EventArgs.Empty));
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
                catch { }
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
        AppLogger.Log($"MediaPlayer MediaOpened. Duration: {_mediaPlayer.PlaybackSession.NaturalDuration}");
        _dispatcherQueue.TryEnqueue(() =>
        {
            PlaybackStateChanged?.Invoke(this, _mediaPlayer.PlaybackSession.PlaybackState);
        });
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        AppLogger.Log($"MediaPlayer FAILED: {args.Error} - {args.ErrorMessage} (HResult: 0x{args.ExtendedErrorCode:X8})");
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        AppLogger.Log($"PlaybackStateChanged: {sender.PlaybackState}");
        _dispatcherQueue.TryEnqueue(() =>
        {
            PlaybackStateChanged?.Invoke(this, sender.PlaybackState);
        });
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        _dispatcherQueue.TryEnqueue(async () =>
        {
            PlaybackStateChanged?.Invoke(this, MediaPlaybackState.None);

            if (RepeatMode == 2)
            {
                Seek(TimeSpan.Zero);
                Resume();
            }
            else
            {
                await PlayNextAsync();
            }
        });
    }

    private void OnSmtcButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        _dispatcherQueue.TryEnqueue(async () =>
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
                case SystemMediaTransportControlsButton.Next:
                    await PlayNextAsync();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    await PlayPreviousAsync();
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
