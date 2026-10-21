using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using YTMusicClient.Helpers;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public class YouTubeMusicService : IYouTubeMusicService
{
    private readonly YoutubeClient _youtube;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(6) };

    public YouTubeMusicService()
    {
        _youtube = new YoutubeClient();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = new List<SearchResult>();
        try
        {
            AppLogger.Log($"SearchAsync: '{query}'");
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
            AppLogger.Log($"SearchAsync: Found {results.Count} results");
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"SearchAsync failed for '{query}'", ex);
        }
        return results;
    }

    public async Task<Song> GetSongDetailsAsync(string videoId, CancellationToken ct = default)
    {
        try
        {
            AppLogger.Log($"GetSongDetailsAsync: {videoId}");
            var streamUrl = await GetAudioStreamUrlAsync(videoId, ct);

            string title = "Song";
            string artist = "Artist";
            string thumb = string.Empty;
            TimeSpan duration = TimeSpan.Zero;

            try
            {
                var video = await _youtube.Videos.GetAsync(videoId, ct);
                title = video.Title;
                artist = video.Author.ChannelTitle;
                thumb = video.Thumbnails.Count > 0
                    ? video.Thumbnails.OrderByDescending(t => t.Resolution.Area).First().Url
                    : string.Empty;
                duration = video.Duration ?? TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                AppLogger.Log($"Video metadata fetch fallback for {videoId}: {ex.Message}");
            }

            return new Song
            {
                VideoId = videoId,
                Title = title,
                Artist = artist,
                ThumbnailUrl = thumb,
                Duration = duration,
                StreamUrl = streamUrl
            };
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"GetSongDetailsAsync({videoId})", ex);
            throw;
        }
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoId, CancellationToken ct = default)
    {
        // 1. Try YoutubeExplode native extraction
        try
        {
            AppLogger.Log($"GetAudioStreamUrlAsync: Fetching manifest for {videoId}");
            var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId, ct);

            // 1. Prefer MP4/M4A AAC audio streams
            var audioStream = manifest.GetAudioOnlyStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (audioStream != null)
            {
                AppLogger.Log($"Selected YoutubeExplode MP4 stream: {audioStream.Bitrate} bps");
                return audioStream.Url;
            }

            // 2. Fallback to muxed MP4 streams
            var muxedStream = manifest.GetMuxedStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (muxedStream != null)
            {
                AppLogger.Log($"Selected YoutubeExplode muxed MP4 stream: {muxedStream.Bitrate} bps");
                return muxedStream.Url;
            }

            // 3. Fallback to any audio stream
            var fallbackStream = manifest.GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (fallbackStream != null)
            {
                AppLogger.Log($"Selected YoutubeExplode fallback audio stream: {fallbackStream.Container} - {fallbackStream.Bitrate} bps");
                return fallbackStream.Url;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"YoutubeExplode manifest extraction failed for {videoId}", ex);
        }

        // 2. Resilient Fallback Engine: Invidious / Piped API
        AppLogger.Log($"Engaging resilient fallback stream resolver for {videoId}...");
        var fallbackUrl = await TryGetFallbackStreamUrlAsync(videoId, ct);
        if (!string.IsNullOrEmpty(fallbackUrl))
        {
            return fallbackUrl;
        }

        AppLogger.Log($"All stream extraction methods exhausted for {videoId}");
        return string.Empty;
    }

    private async Task<string> TryGetFallbackStreamUrlAsync(string videoId, CancellationToken ct)
    {
        var endpoints = new[]
        {
            $"https://api.piped.privacydev.net/streams/{videoId}",
            $"https://pipedapi.kavin.rocks/streams/{videoId}",
            $"https://pipedapi.tokhmi.xyz/streams/{videoId}"
        };

        foreach (var url in endpoints)
        {
            try
            {
                AppLogger.Log($"Querying fallback endpoint: {url}");
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                using var response = await _httpClient.SendAsync(req, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("audioStreams", out var audioStreams) && audioStreams.GetArrayLength() > 0)
                    {
                        // Prefer MP4 / AAC
                        foreach (var s in audioStreams.EnumerateArray())
                        {
                            if (s.TryGetProperty("mimeType", out var mime) && mime.GetString()?.StartsWith("audio/mp4") == true &&
                                s.TryGetProperty("url", out var streamUrl))
                            {
                                var resolved = streamUrl.GetString();
                                if (!string.IsNullOrEmpty(resolved))
                                {
                                    AppLogger.Log($"Fallback SUCCESS: Got MP4 audio stream from {url}");
                                    return resolved;
                                }
                            }
                        }

                        // Any audio stream
                        var first = audioStreams[0];
                        if (first.TryGetProperty("url", out var anyUrl))
                        {
                            var resolved = anyUrl.GetString();
                            if (!string.IsNullOrEmpty(resolved))
                            {
                                AppLogger.Log($"Fallback SUCCESS: Got generic audio stream from {url}");
                                return resolved;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"Fallback endpoint {url} failed: {ex.Message}");
            }
        }
        return string.Empty;
    }

    public async Task<IReadOnlyList<SearchResult>> GetTrendingAsync(CancellationToken ct = default)
    {
        return await SearchAsync("trending music 2024", ct);
    }
}
