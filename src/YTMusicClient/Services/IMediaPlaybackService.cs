using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Media.Playback;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public interface IMediaPlaybackService
{
    MediaPlaybackState PlaybackState { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    Song? CurrentSong { get; }
    bool IsPlaying { get; }
    double PlaybackRate { get; set; }
    bool IsShuffleEnabled { get; set; }
    int RepeatMode { get; set; } // 0 = Off, 1 = Repeat All, 2 = Repeat One

    ObservableCollection<Song> UpNext { get; }
    ObservableCollection<Song> History { get; }

    event EventHandler<Song>? SongChanged;
    event EventHandler<MediaPlaybackState>? PlaybackStateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler? QueueChanged;

    Task PlayAsync(Song song);
    void SetCurrentSong(Song song);
    void Pause();
    void Resume();
    void TogglePlayPause();
    void Stop();
    void Seek(TimeSpan position);

    Task PlayNextAsync();
    Task PlayPreviousAsync();
    void Enqueue(Song song);
    void EnqueueNext(Song song);
    void RemoveFromQueue(Song song);
    void ClearQueue();
}
