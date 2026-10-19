using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public class YouTubeMusicService : IYouTubeMusicService
{
    private readonly YoutubeClient _youtube;

    public YouTubeMusicService()
    {
        _youtube = new YoutubeClient();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = new List<SearchResult>();
        try
        {
            await foreach (var result in _youtube.Search.GetVideosAsync(query, ct))
            {
                results.Add(new SearchResult
                {
                    VideoId = result.Id,
                    Title = result.Title,
                    Artist = result.Author.ChannelTitle,
                    ThumbnailUrl = result.Thumbnails.Count > 0
                        ? result.Thumbnails.OrderByDescending(t => t.Resolution.Area).First().Url
                        : string.Empty,
                    Duration = result.Duration ?? TimeSpan.Zero
                });

                if (results.Count >= 20) break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Search failed: {ex.Message}");
        }
        return results;
    }

    public async Task<Song> GetSongDetailsAsync(string videoId, CancellationToken ct = default)
    {
        try
        {
            var video = await _youtube.Videos.GetAsync(videoId, ct);
            var streamUrl = await GetAudioStreamUrlAsync(videoId, ct);

            return new Song
            {
                VideoId = video.Id,
                Title = video.Title,
                Artist = video.Author.ChannelTitle,
                ThumbnailUrl = video.Thumbnails.Count > 0
                    ? video.Thumbnails.OrderByDescending(t => t.Resolution.Area).First().Url
                    : string.Empty,
                Duration = video.Duration ?? TimeSpan.Zero,
                StreamUrl = streamUrl
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetSongDetails failed: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoId, CancellationToken ct = default)
    {
        try
        {
            var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId, ct);
            var streamInfo = manifest.GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            return streamInfo?.Url ?? string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetAudioStreamUrl failed: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<IReadOnlyList<SearchResult>> GetTrendingAsync(CancellationToken ct = default)
    {
        // Search for trending music as a proxy since InnerTube trending endpoint requires auth
        return await SearchAsync("trending music 2024", ct);
    }
}
