using YTMusicClient.Models;

namespace YTMusicClient.Services;

public interface ILyricsService
{
    Task<IReadOnlyList<LyricLine>> GetSyncedLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        string? videoId = null,
        CancellationToken ct = default);

    Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        CancellationToken ct = default);
}
