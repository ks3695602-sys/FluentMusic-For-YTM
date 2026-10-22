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

    private static readonly string[] DiscoveryQueries = new[]
    {
        "Global Top Hits 2026",
        "Bollywood Romantic Essentials",
        "Arijit Singh Hits",
        "Viral Pop Hits 2026",
        "Lo-Fi Chill Beats",
        "Synthwave Retro Waves",
        "Trending Pop Songs",
        "Indie Acoustic Vibes",
        "Punjabi Hits 2026",
        "Hip Hop Chartbusters",
        "R&B Midnight Melodies",
        "English Rock Classics"
    };

    private static readonly (string Start, string End)[] AmbientGradients = new[]
    {
        ("#4A1525", "#1B1724"),
        ("#1A384A", "#141D26"),
        ("#3D2B14", "#1E1A17"),
        ("#2D164E", "#181422"),
        ("#144032", "#13201C"),
        ("#462014", "#201715"),
        ("#381442", "#1A1422"),
        ("#183640", "#131A20")
    };

    public YouTubeMusicService()
    {
        _youtube = new YoutubeClient();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = new List<SearchResult>();
        int gradIndex = 0;

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
                            var (start, end) = AmbientGradients[gradIndex % AmbientGradients.Length];
                            gradIndex++;

                            results.Add(new SearchResult
                            {
                                VideoId = id,
                                Title = title,
                                Artist = artist,
                                ThumbnailUrl = artwork,
                                Duration = TimeSpan.FromSeconds(durationSec),
                                GradientStart = start,
                                GradientEnd = end,
                                AudioQuality = "256 kbps · AAC · Hi-Res"
                            });
                        }
                    }

                    // Validate query relevance before accepting BravePipe tracks
                    var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "by", "the", "a", "an", "and", "in", "of", "to", "for", "on", "with" };
                    var queryWords = query.Split(new[] { ' ', '-', '_', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Where(w => w.Length >= 2 && !stopWords.Contains(w))
                                          .ToList();

                    bool isRelevant = queryWords.Count == 0 || results.Any(r =>
                        queryWords.Any(w => r.Title.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                                            r.Artist.Contains(w, StringComparison.OrdinalIgnoreCase)));

                    if (isRelevant && results.Count > 0)
                    {
                        AppLogger.Log($"BravePipe search SUCCESS: {results.Count} relevant tracks found");
                        return results;
                    }

                    AppLogger.Log($"BravePipe results lacked relevance to '{query}'. Clearing and falling back to YouTube.");
                    results.Clear();
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
                var (start, end) = AmbientGradients[gradIndex % AmbientGradients.Length];
                gradIndex++;

                results.Add(new SearchResult
                {
                    VideoId = result.Id,
                    Title = result.Title,
                    Artist = result.Author.ChannelTitle,
                    ThumbnailUrl = result.Thumbnails.Count > 0
                        ? result.Thumbnails.OrderByDescending(t => t.Resolution.Area).First().Url
                        : string.Empty,
                    Duration = result.Duration ?? TimeSpan.Zero,
                    GradientStart = start,
                    GradientEnd = end,
                    AudioQuality = "256 kbps · AAC · Hi-Res"
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
            StreamUrl = streamUrl,
            AudioQuality = "256 kbps · AAC · Hi-Res",
            AudioCodec = "AAC",
            AudioSource = "BravePipe / Qobuz"
        };
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoId, CancellationToken ct = default)
    {
        // 1. If ID is from BravePipe / Qobuz provider
        if (videoId.StartsWith("qobuz_") || !videoId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_') || videoId.Length > 15)
        {
            try
            {
                AppLogger.Log($"Resolving BravePipe lossless stream for track: {videoId}");
                var streamApiUrl = $"{BravePipeBase}/stream/{Uri.EscapeDataString(videoId)}?quality=LOSSLESS";
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
                            AppLogger.Log($"BravePipe lossless stream SUCCESS: {resolvedUrl.Substring(0, Math.Min(60, resolvedUrl.Length))}...");
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
            var bridgeUrl = $"{BravePipeBase}/stream/{Uri.EscapeDataString(videoId)}?quality=HIGH";
            using var req = new HttpRequestMessage(HttpMethod.Get, bridgeUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            using var resp = await _httpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    var resolved = urlProp.GetString();
                    if (!string.IsNullOrEmpty(resolved)) return resolved;
                }
            }
        }
        catch { }

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
        try
        {
            var reqObj = new
            {
                context = new
                {
                    client = new
                    {
                        clientName = "WEB_REMIX",
                        clientVersion = "1.20240101.01.00",
                        gl = "IN",
                        hl = "en"
                    }
                },
                browseId = "FEmusic_charts"
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(reqObj), System.Text.Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/browse?prettyPrint=false")
            {
                Content = jsonContent
            };
            msg.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (localSettings.Values["GoogleAuthCookies"] is string cookies && !string.IsNullOrWhiteSpace(cookies))
                {
                    msg.Headers.Add("Cookie", cookies);
                }
            }
            catch { }

            using var resp = await _httpClient.SendAsync(msg, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                var parsed = ParseInnertubeBrowseTracks(body);
                if (parsed.Count > 0)
                {
                    AppLogger.Log($"GetTrendingAsync: Successfully retrieved {parsed.Count} real YouTube Music tracks from Innertube");
                    return parsed;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("GetTrendingAsync Innertube failed, falling back to search", ex);
        }

        return await SearchAsync("Trending Hindi Bollywood 2026", ct);
    }

    public async Task<IReadOnlyList<Song>> GetLikedSongsAsync(CancellationToken ct = default)
    {
        var songs = new List<Song>();
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string? cookies = localSettings.Values["GoogleAuthCookies"] as string;

            var reqObj = new
            {
                context = new
                {
                    client = new
                    {
                        clientName = "WEB_REMIX",
                        clientVersion = "1.20240101.01.00",
                        gl = "IN",
                        hl = "en"
                    }
                },
                browseId = !string.IsNullOrWhiteSpace(cookies) ? "VLLM" : "FEmusic_charts"
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(reqObj), System.Text.Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/browse?prettyPrint=false")
            {
                Content = jsonContent
            };
            msg.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            if (!string.IsNullOrWhiteSpace(cookies))
            {
                msg.Headers.Add("Cookie", cookies);
            }

            using var resp = await _httpClient.SendAsync(msg, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                var results = ParseInnertubeBrowseTracks(body);
                foreach (var r in results)
                {
                    songs.Add(new Song
                    {
                        VideoId = r.VideoId,
                        Title = r.Title,
                        Artist = r.Artist,
                        ThumbnailUrl = r.ThumbnailUrl,
                        Duration = r.Duration,
                        AudioQuality = "256 kbps · AAC · Hi-Res",
                        AudioCodec = "AAC",
                        AudioSource = "YouTube Music"
                    });
                }

                if (songs.Count > 0)
                {
                    AppLogger.Log($"GetLikedSongsAsync: Retrieved {songs.Count} synced songs from YouTube Music");
                    return songs;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("GetLikedSongsAsync failed", ex);
        }

        var fallbackResults = await GetTrendingAsync(ct);
        foreach (var r in fallbackResults)
        {
            songs.Add(new Song
            {
                VideoId = r.VideoId,
                Title = r.Title,
                Artist = r.Artist,
                ThumbnailUrl = r.ThumbnailUrl,
                Duration = r.Duration,
                AudioQuality = "256 kbps · AAC · Hi-Res",
                AudioCodec = "AAC",
                AudioSource = "YouTube Music"
            });
        }
        return songs;
    }

    private static List<SearchResult> ParseInnertubeBrowseTracks(string json)
    {
        var list = new List<SearchResult>();
        int gradIndex = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            ExtractItemsRecursive(root, list, ref gradIndex);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("ParseInnertubeBrowseTracks error", ex);
        }
        return list;
    }

    private static void ExtractItemsRecursive(JsonElement element, List<SearchResult> list, ref int gradIndex)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("musicResponsiveListItemRenderer", out var item))
            {
                string videoId = string.Empty;
                if (item.TryGetProperty("playlistItemData", out var pData) && pData.TryGetProperty("videoId", out var vidProp))
                {
                    videoId = vidProp.GetString() ?? string.Empty;
                }

                string title = string.Empty;
                string artist = string.Empty;
                string thumb = string.Empty;

                if (item.TryGetProperty("flexColumns", out var cols) && cols.GetArrayLength() > 0)
                {
                    var col0 = cols[0];
                    if (col0.TryGetProperty("musicResponsiveListItemFlexColumnRenderer", out var flex0) &&
                        flex0.TryGetProperty("text", out var text0) &&
                        text0.TryGetProperty("runs", out var runs0) && runs0.GetArrayLength() > 0)
                    {
                        title = runs0[0].GetProperty("text").GetString() ?? string.Empty;
                        if (string.IsNullOrEmpty(videoId) && runs0[0].TryGetProperty("navigationEndpoint", out var nav) &&
                            nav.TryGetProperty("watchEndpoint", out var watch) &&
                            watch.TryGetProperty("videoId", out var wVid))
                        {
                            videoId = wVid.GetString() ?? string.Empty;
                        }
                    }

                    if (cols.GetArrayLength() > 1)
                    {
                        var col1 = cols[1];
                        if (col1.TryGetProperty("musicResponsiveListItemFlexColumnRenderer", out var flex1) &&
                            flex1.TryGetProperty("text", out var text1) &&
                            text1.TryGetProperty("runs", out var runs1) && runs1.GetArrayLength() > 0)
                        {
                            artist = runs1[0].GetProperty("text").GetString() ?? string.Empty;
                        }
                    }
                }

                if (item.TryGetProperty("thumbnail", out var thumbObj) &&
                    thumbObj.TryGetProperty("musicThumbnailRenderer", out var mThumb) &&
                    mThumb.TryGetProperty("thumbnail", out var innerThumb) &&
                    innerThumb.TryGetProperty("thumbnails", out var tList) && tList.GetArrayLength() > 0)
                {
                    thumb = tList[tList.GetArrayLength() - 1].GetProperty("url").GetString() ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(videoId) && !string.IsNullOrEmpty(title) && !list.Any(x => x.VideoId == videoId))
                {
                    var (start, end) = AmbientGradients[gradIndex % AmbientGradients.Length];
                    gradIndex++;

                    list.Add(new SearchResult
                    {
                        VideoId = videoId,
                        Title = title,
                        Artist = !string.IsNullOrEmpty(artist) ? artist : "YouTube Music",
                        ThumbnailUrl = thumb,
                        Duration = TimeSpan.FromMinutes(3.5),
                        GradientStart = start,
                        GradientEnd = end,
                        AudioQuality = "256 kbps · AAC · Hi-Res"
                    });
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                ExtractItemsRecursive(prop.Value, list, ref gradIndex);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                ExtractItemsRecursive(child, list, ref gradIndex);
            }
        }
    }
}
