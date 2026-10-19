using YTMusicClient.Models;

namespace YTMusicClient.Services;

public interface IYouTubeMusicService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct = default);
    Task<Song> GetSongDetailsAsync(string videoId, CancellationToken ct = default);
    Task<string> GetAudioStreamUrlAsync(string videoId, CancellationToken ct = default);
    Task<IReadOnlyList<SearchResult>> GetTrendingAsync(CancellationToken ct = default);
}
