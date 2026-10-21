using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using YTMusicClient.Helpers;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public class YouTubeMusicService : IYouTubeMusicService
{
    private readonly YoutubeClient _youtube;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private const string BravePipeBase = "https://bppcaf.bot-keep.xyz";

    public YouTubeMusicService()
    {
        _youtube = new YoutubeClient();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = new List<SearchResult>();

        // 1. Try BravePipe / Bot-Keep API first (High Quality & Instant Results)
        try
        {
            AppLogger.Log($"SearchAsync querying BravePipe for: '{query}'");
            var searchUrl = $"{BravePipeBase}/search?q={Uri.EscapeDataString(query)}&quality=HIGH";
            using var req = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            using var resp = await _httpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tracks", out var tracks) && tracks.GetArrayLength() > 0)
                {
                    foreach (var t in tracks.EnumerateArray())
                    {
                        var id = t.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
                        var title = t.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Unknown Title" : "Unknown Title";
                        var artist = t.TryGetProperty("artist", out var artProp) ? artProp.GetString() ?? "Unknown Artist" : "Unknown Artist";
                        var artwork = t.TryGetProperty("artworkURL", out var artUrlProp) ? artUrlProp.GetString() ?? string.Empty : string.Empty;
                        var durationSec = t.TryGetProperty("duration", out var durProp) && durProp.TryGetInt32(out var sec) ? sec : 0;

                        if (!string.IsNullOrEmpty(id))
                        {
                            results.Add(new SearchResult
                            {
                                VideoId = id,
                                Title = title,
                                Artist = artist,
                                ThumbnailUrl = artwork,
                                Duration = TimeSpan.FromSeconds(durationSec)
                            });
                        }
                    }
                    AppLogger.Log($"BravePipe search SUCCESS: {results.Count} tracks found");
                    return results;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"BravePipe search failed: {ex.Message}");
        }

        // 2. Fallback to YoutubeExplode Search
        try
        {
            AppLogger.Log($"Fallback to YoutubeExplode search for: '{query}'");
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
            AppLogger.LogError($"YoutubeExplode search failed for '{query}'", ex);
        }

        return results;
    }

    public async Task<Song> GetSongDetailsAsync(string videoId, CancellationToken ct = default)
    {
        AppLogger.Log($"GetSongDetailsAsync: {videoId}");
        var streamUrl = await GetAudioStreamUrlAsync(videoId, ct);

        return new Song
        {
            VideoId = videoId,
            Title = "Playing Song",
            Artist = "Artist",
            ThumbnailUrl = string.Empty,
            Duration = TimeSpan.Zero,
            StreamUrl = streamUrl
        };
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoId, CancellationToken ct = default)
    {
        // 1. If ID is from BravePipe / Qobuz provider
        if (videoId.StartsWith("qobuz_") || !videoId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_') || videoId.Length > 15)
        {
            try
            {
                AppLogger.Log($"Resolving BravePipe stream for track: {videoId}");
                var streamApiUrl = $"{BravePipeBase}/stream/{Uri.EscapeDataString(videoId)}?quality=HIGH";
                using var req = new HttpRequestMessage(HttpMethod.Get, streamApiUrl);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                using var resp = await _httpClient.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("url", out var urlProp))
                    {
                        var resolvedUrl = urlProp.GetString();
                        if (!string.IsNullOrEmpty(resolvedUrl))
                        {
                            AppLogger.Log($"BravePipe stream SUCCESS: {resolvedUrl.Substring(0, Math.Min(60, resolvedUrl.Length))}...");
                            return resolvedUrl;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"BravePipe stream failed for {videoId}", ex);
            }
        }

        // 2. Try YoutubeExplode native extraction
        try
        {
            AppLogger.Log($"Fetching YoutubeExplode manifest for {videoId}");
            var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId, ct);

            // Prefer MP4 AAC stream
            var audioStream = manifest.GetAudioOnlyStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (audioStream != null)
            {
                AppLogger.Log($"Selected YoutubeExplode MP4 stream: {audioStream.Bitrate} bps");
                return audioStream.Url;
            }

            // Fallback muxed MP4
            var muxedStream = manifest.GetMuxedStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (muxedStream != null)
            {
                return muxedStream.Url;
            }

            // Fallback any stream
            var fallbackStream = manifest.GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (fallbackStream != null)
            {
                return fallbackStream.Url;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"YoutubeExplode failed for {videoId}", ex);
        }

        // 3. Fallback to BravePipe YouTube stream bridge
        try
        {
            AppLogger.Log($"Trying BravePipe search stream bridge for {videoId}...");
            var bpSearchUrl = $"{BravePipeBase}/search?q={Uri.EscapeDataString(videoId)}&quality=HIGH";
            using var req = new HttpRequestMessage(HttpMethod.Get, bpSearchUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            using var resp = await _httpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tracks", out var tracks) && tracks.GetArrayLength() > 0)
                {
                    var firstTrack = tracks[0];
                    if (firstTrack.TryGetProperty("id", out var trackIdProp))
                    {
                        var trackId = trackIdProp.GetString();
                        if (!string.IsNullOrEmpty(trackId))
                        {
                            return await GetAudioStreamUrlAsync(trackId, ct);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"BravePipe search bridge fallback failed: {ex.Message}");
        }

        // 4. Fallback to Piped APIs
        var pipedEndpoints = new[]
        {
            $"https://api.piped.privacydev.net/streams/{videoId}",
            $"https://pipedapi.kavin.rocks/streams/{videoId}"
        };

        foreach (var url in pipedEndpoints)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                using var response = await _httpClient.SendAsync(req, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("audioStreams", out var audioStreams) && audioStreams.GetArrayLength() > 0)
                    {
                        foreach (var s in audioStreams.EnumerateArray())
                        {
                            if (s.TryGetProperty("url", out var streamUrl))
                            {
                                var resolved = streamUrl.GetString();
                                if (!string.IsNullOrEmpty(resolved))
                                {
                                    AppLogger.Log($"Piped fallback SUCCESS from {url}");
                                    return resolved;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        AppLogger.Log($"All streaming sources exhausted for {videoId}");
        return string.Empty;
    }

    public async Task<IReadOnlyList<SearchResult>> GetTrendingAsync(CancellationToken ct = default)
    {
        // Search popular top tracks via BravePipe / YouTube
        return await SearchAsync("Top Hits 2026", ct);
    }
}
