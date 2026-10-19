using YTMusicClient.Models;

namespace YTMusicClient.Services;

public interface ILyricsService
{
    Task<IReadOnlyList<LyricLine>> GetSyncedLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken ct = default);
}
