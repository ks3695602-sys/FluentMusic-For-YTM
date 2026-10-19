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

    event EventHandler<Song>? SongChanged;
    event EventHandler<MediaPlaybackState>? PlaybackStateChanged;
    event EventHandler<TimeSpan>? PositionChanged;

    Task PlayAsync(Song song);
    void Pause();
    void Resume();
    void TogglePlayPause();
    void Stop();
    void Seek(TimeSpan position);
}
