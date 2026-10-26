using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public interface IYouTubeMusicService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct = default);
    Task<Song> GetSongDetailsAsync(string videoId, CancellationToken ct = default);
    Task<string> GetAudioStreamUrlAsync(string videoId, CancellationToken ct = default);
    Task<string> GetLosslessStreamUrlAsync(string videoId, string title, string artist, CancellationToken ct = default);
    Task<IReadOnlyList<SearchResult>> GetTrendingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Song>> GetLikedSongsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Playlist>> GetLibraryPlaylistsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Song>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default);
    Task<HomeFeedResult> GetPersonalizedHomeFeedAsync(string? moodParam = null, CancellationToken ct = default);
    Task<IReadOnlyList<Song>> GetRadioTracksAsync(string videoId, string title, string artist, CancellationToken ct = default);
}
