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

    private bool _isShuffleEnabled;
    public bool IsShuffleEnabled
    {
        get => _isShuffleEnabled;
        set
        {
            _isShuffleEnabled = value;
            if (_isShuffleEnabled)
            {
                ShuffleQueue();
            }
        }
    }
    public int RepeatMode { get; set; } // 0 = Off, 1 = Repeat All, 2 = Repeat One
    public double CrossfadeDurationSeconds { get; set; } = 4.0;
    public bool IsVolumeNormalizationEnabled { get; set; } = true;
    private double _baseVolume = 1.0;

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

        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values["CrossfadeSeconds"] is double cf) CrossfadeDurationSeconds = cf;
            if (localSettings.Values["VolumeNormalization"] is bool vn) IsVolumeNormalizationEnabled = vn;
        }
        catch { }

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

        // High-precision position timer - fires every 40ms (~25 FPS beacon) for silky-smooth lyrics
        _positionTimer = new System.Threading.Timer(OnPositionTimerTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(40));
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
                var ext = System.IO.Path.GetExtension(song.LocalFilePath).ToLowerInvariant();
                song.AudioCodec = ext switch
                {
                    ".flac" => "FLAC",
                    ".alac" => "ALAC",
                    ".wav" => "WAV",
                    ".opus" => "OPUS",
                    ".ogg" => "OGG",
                    ".mp3" => "MP3",
                    _ => "AAC"
                };
                if (song.AudioCodec is "FLAC" or "ALAC" or "WAV")
                {
                    song.AudioQuality = "1411 kbps · " + song.AudioCodec + " · Lossless";
                    song.FidelityTier = "Hi-Res Lossless";
                }
                else
                {
                    song.AudioQuality = "256 kbps · " + song.AudioCodec + " · Hi-Res";
                    song.FidelityTier = "Hi-Res";
                }
                SetCurrentSong(song);

                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(song.LocalFilePath);
                _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
                _mediaPlayer.Play();
                await UpdateSmtcDisplayAsync(song);
                return;
            }

            // Ensure valid 11-char videoId for YouTube resolution
            bool isValid11CharId = !string.IsNullOrEmpty(song.VideoId) && 
                                  song.VideoId.Length == 11 && 
                                  song.VideoId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

            if (!isValid11CharId)
            {
                try
                {
                    var musicService = App.Services.GetService(typeof(IYouTubeMusicService)) as IYouTubeMusicService;
                    if (musicService != null)
                    {
                        var q = $"{song.Title} {song.Artist}".Trim();
                        AppLogger.Log($"Resolving real 11-char videoId for non-direct track: '{q}'");
                        var results = await musicService.SearchAsync(q);
                        if (results.Count > 0)
                        {
                            song.VideoId = results[0].VideoId;
                            if (string.IsNullOrEmpty(song.ThumbnailUrl))
                            {
                                song.ThumbnailUrl = results[0].ThumbnailUrl;
                            }
                            // Re-notify song change so lyrics and downstream services receive the resolved 11-char videoId immediately
                            SetCurrentSong(song);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Failed to resolve fallback videoId for track", ex);
                }
            }

            if (string.IsNullOrEmpty(song.StreamUrl))
            {
                // If stream URL is not preloaded, fetch via YouTubeMusicService
                try
                {
                    var musicService = App.Services.GetService(typeof(IYouTubeMusicService)) as IYouTubeMusicService;
                    if (musicService != null)
                    {
                        song.StreamUrl = await musicService.GetLosslessStreamUrlAsync(song.VideoId, song.Title, song.Artist);
                        if (string.IsNullOrEmpty(song.StreamUrl))
                        {
                            song.StreamUrl = await musicService.GetAudioStreamUrlAsync(song.VideoId);
                        }

                        if (!string.IsNullOrEmpty(song.StreamUrl))
                        {
                            if (song.StreamUrl.Contains(".flac", StringComparison.OrdinalIgnoreCase) || 
                                song.StreamUrl.Contains("lossless", StringComparison.OrdinalIgnoreCase) ||
                                song.StreamUrl.Contains("qobuz", StringComparison.OrdinalIgnoreCase))
                            {
                                song.AudioCodec = "FLAC";
                                song.AudioQuality = "1411 kbps · FLAC · Lossless";
                                song.FidelityTier = "Hi-Res Lossless";
                                song.AudioSource = "Qobuz Lossless";
                            }
                            else
                            {
                                song.AudioCodec = "AAC";
                                song.AudioQuality = "256 kbps · AAC · Hi-Res";
                                song.FidelityTier = "Hi-Res";
                                song.AudioSource = "YouTube High-Res";
                            }
                            SetCurrentSong(song);
                        }
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

            string playUrl = song.StreamUrl;
            if (playUrl.Contains("googlevideo.com", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var proxy = App.Services.GetService(typeof(LocalMediaProxy)) as LocalMediaProxy;
                    if (proxy != null)
                    {
                        playUrl = proxy.GetProxiedStreamUrl(playUrl);
                    }
                }
                catch { }
            }

            AppLogger.Log($"PlayAsync: Setting MediaPlayer source to {playUrl.Substring(0, Math.Min(60, playUrl.Length))}...");
            _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(playUrl));
            _mediaPlayer.Play();
            AppLogger.Log("PlayAsync: MediaPlayer.Play() invoked");

            // Update SMTC display
            await UpdateSmtcDisplayAsync(song);

            // Populate taste-based radio queue dynamically
            _ = EnsureRadioQueueAsync();
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
        if (UpNext.Count == 0)
        {
            await EnsureRadioQueueAsync();
        }

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

    public async Task EnsureRadioQueueAsync()
    {
        if (UpNext.Count >= 4 || CurrentSong == null) return;
        try
        {
            var ytService = App.Services.GetService(typeof(IYouTubeMusicService)) as IYouTubeMusicService;
            if (ytService != null)
            {
                var radioTracks = await ytService.GetRadioTracksAsync(CurrentSong.VideoId, CurrentSong.Title, CurrentSong.Artist);
                if (radioTracks != null && radioTracks.Count > 0)
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        foreach (var track in radioTracks)
                        {
                            if (track.VideoId != CurrentSong.VideoId && !UpNext.Any(s => s.VideoId == track.VideoId))
                            {
                                UpNext.Add(track);
                            }
                        }
                        if (_isShuffleEnabled)
                        {
                            ShuffleQueue();
                        }
                        QueueChanged?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("EnsureRadioQueueAsync failed", ex);
        }
    }

    public void ShuffleQueue()
    {
        if (UpNext.Count <= 1) return;
        var rng = new Random();
        int n = UpNext.Count;
        var list = UpNext.ToList();
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            var value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
        UpNext.Clear();
        foreach (var item in list) UpNext.Add(item);
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
