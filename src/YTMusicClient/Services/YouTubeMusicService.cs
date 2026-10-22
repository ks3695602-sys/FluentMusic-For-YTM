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

    public async Task<string> GetLosslessStreamUrlAsync(string videoId, string title, string artist, CancellationToken ct = default)
    {
        return await GetAudioStreamUrlCoreAsync(videoId, title, artist, ct);
    }

    public async Task<string> GetAudioStreamUrlAsync(string videoId, CancellationToken ct = default)
    {
        return await GetAudioStreamUrlCoreAsync(videoId, null, null, ct);
    }

    private async Task<string> GetAudioStreamUrlCoreAsync(string videoId, string? title, string? artist, CancellationToken ct)
    {
        // 1. If Title is provided, try Qobuz lossless bridge FIRST
        if (!string.IsNullOrWhiteSpace(title))
        {
            try
            {
                string searchLosslessQuery = $"{title} {artist}".Trim();
                AppLogger.Log($"Resolving Qobuz Lossless FLAC stream for: '{searchLosslessQuery}'");
                var searchUrl = $"{BravePipeBase}/search?q={Uri.EscapeDataString(searchLosslessQuery)}&quality=LOSSLESS";
                using var req = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                using var resp = await _httpClient.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        var first = doc.RootElement[0];
                        string? qId = null;
                        if (first.TryGetProperty("id", out var idProp)) qId = idProp.GetString();
                        else if (first.TryGetProperty("videoId", out var vProp)) qId = vProp.GetString();

                        if (!string.IsNullOrEmpty(qId))
                        {
                            var streamUrl = $"{BravePipeBase}/stream/{Uri.EscapeDataString(qId)}?quality=LOSSLESS";
                            using var sReq = new HttpRequestMessage(HttpMethod.Get, streamUrl);
                            sReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                            using var sResp = await _httpClient.SendAsync(sReq, ct);
                            if (sResp.IsSuccessStatusCode)
                            {
                                var sJson = await sResp.Content.ReadAsStringAsync(ct);
                                using var sDoc = JsonDocument.Parse(sJson);
                                if (sDoc.RootElement.TryGetProperty("url", out var uProp))
                                {
                                    var flacUrl = uProp.GetString();
                                    if (!string.IsNullOrEmpty(flacUrl))
                                    {
                                        AppLogger.Log($"Qobuz Lossless FLAC stream SUCCESS for {title}: {flacUrl.Substring(0, Math.Min(60, flacUrl.Length))}...");
                                        return flacUrl;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Qobuz Lossless stream lookup failed for {title}", ex);
            }
        }

        // 2. If ID is from BravePipe / Qobuz provider
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

    public static void AddAuthorizationHeaders(HttpRequestMessage msg, string? cookies)
    {
        if (string.IsNullOrWhiteSpace(cookies)) return;
        msg.Headers.TryAddWithoutValidation("Cookie", cookies);

        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(cookies, @"(?:SAPISID|__Secure-3PAPISID)=([^;]+)");
            if (match.Success)
            {
                var sapisid = match.Groups[1].Value.Trim();
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string origin = "https://music.youtube.com";
                string input = $"{timestamp} {sapisid} {origin}";
                using var sha1 = System.Security.Cryptography.SHA1.Create();
                var hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                msg.Headers.TryAddWithoutValidation("Authorization", $"SAPISIDHASH {timestamp}_{hashHex}");
                msg.Headers.TryAddWithoutValidation("X-Origin", origin);
            }
        }
        catch { }
    }

    public async Task<HomeFeedResult> GetPersonalizedHomeFeedAsync(string? moodParam = null, CancellationToken ct = default)
    {
        var result = new HomeFeedResult();
        try
        {
            string? cookies = null;
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                cookies = localSettings.Values["GoogleAuthCookies"] as string;
            }
            catch { }

            object reqObj;
            if (!string.IsNullOrEmpty(moodParam))
            {
                reqObj = new
                {
                    context = new
                    {
                        client = new
                        {
                            clientName = "WEB_REMIX",
                            clientVersion = "1.20240101.01.00",
                            gl = "IN",
                            hl = "hi"
                        }
                    },
                    browseId = "FEmusic_home",
                    @params = moodParam
                };
            }
            else
            {
                reqObj = new
                {
                    context = new
                    {
                        client = new
                        {
                            clientName = "WEB_REMIX",
                            clientVersion = "1.20240101.01.00",
                            gl = "IN",
                            hl = "hi"
                        }
                    },
                    browseId = "FEmusic_home"
                };
            }

            var jsonContent = new StringContent(JsonSerializer.Serialize(reqObj), System.Text.Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/browse?prettyPrint=false")
            {
                Content = jsonContent
            };
            msg.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            AddAuthorizationHeaders(msg, cookies);

            using var resp = await _httpClient.SendAsync(msg, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                ParseInnertubeHomeFeed(json, result);

                if (result.Shelves.Count > 0)
                {
                    AppLogger.Log($"GetPersonalizedHomeFeedAsync: Retrieved {result.Shelves.Count} shelves, {result.Chips.Count} chips for user {result.UserName}");
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("GetPersonalizedHomeFeedAsync failed", ex);
        }

        // Fallback default feed
        PopulateDefaultFeed(result);
        return result;
    }

    private static void ParseInnertubeHomeFeed(string json, HomeFeedResult feedResult)
    {
        int gradIndex = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 1. Extract Chips
            ExtractChipsRecursive(root, feedResult.Chips);

            // 2. Extract Shelves
            ExtractShelvesRecursive(root, feedResult.Shelves, ref gradIndex, feedResult);

            // Save user profile details if discovered
            if (!string.IsNullOrEmpty(feedResult.UserName))
            {
                try
                {
                    var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["UserDisplayName"] = feedResult.UserName;
                    if (!string.IsNullOrEmpty(feedResult.UserAvatarUrl))
                    {
                        localSettings.Values["UserAvatarUrl"] = feedResult.UserAvatarUrl;
                    }
                    localSettings.Values["IsGoogleSignedIn"] = true;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("ParseInnertubeHomeFeed error", ex);
        }
    }

    private static void ExtractChipsRecursive(JsonElement element, List<MoodChip> chips)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("chipCloudChipRenderer", out var chip))
            {
                string text = string.Empty;
                if (chip.TryGetProperty("text", out var textObj) &&
                    textObj.TryGetProperty("runs", out var runs) && runs.GetArrayLength() > 0)
                {
                    text = runs[0].GetProperty("text").GetString() ?? string.Empty;
                }

                string param = string.Empty;
                if (chip.TryGetProperty("navigationEndpoint", out var nav) &&
                    nav.TryGetProperty("browseEndpoint", out var browse) &&
                    browse.TryGetProperty("params", out var pProp))
                {
                    param = pProp.GetString() ?? string.Empty;
                }

                bool isSelected = chip.TryGetProperty("isSelected", out var sel) && sel.GetBoolean();

                if (!string.IsNullOrEmpty(text) && !chips.Any(c => c.Title == text))
                {
                    chips.Add(new MoodChip
                    {
                        Title = text,
                        Param = param,
                        IsSelected = isSelected
                    });
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                ExtractChipsRecursive(prop.Value, chips);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                ExtractChipsRecursive(child, chips);
            }
        }
    }

    private static void ExtractShelvesRecursive(JsonElement element, List<HomeShelf> shelves, ref int gradIndex, HomeFeedResult feedResult)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("musicCarouselShelfRenderer", out var shelf))
            {
                string title = string.Empty;
                string strapline = string.Empty;

                if (shelf.TryGetProperty("header", out var header) &&
                    header.TryGetProperty("musicCarouselShelfBasicHeaderRenderer", out var bHeader))
                {
                    if (bHeader.TryGetProperty("title", out var titleObj) &&
                        titleObj.TryGetProperty("runs", out var runs) && runs.GetArrayLength() > 0)
                    {
                        title = runs[0].GetProperty("text").GetString() ?? string.Empty;
                    }

                    if (bHeader.TryGetProperty("strapline", out var strapObj) &&
                        strapObj.TryGetProperty("runs", out var sRuns) && sRuns.GetArrayLength() > 0)
                    {
                        strapline = sRuns[0].GetProperty("text").GetString() ?? string.Empty;
                        if (string.IsNullOrEmpty(feedResult.UserName) && !string.IsNullOrWhiteSpace(strapline))
                        {
                            feedResult.UserName = strapline;
                        }
                    }

                    if (bHeader.TryGetProperty("thumbnail", out var tObj) &&
                        tObj.TryGetProperty("musicThumbnailRenderer", out var mT) &&
                        mT.TryGetProperty("thumbnail", out var iT) &&
                        iT.TryGetProperty("thumbnails", out var tArr) && tArr.GetArrayLength() > 0)
                    {
                        feedResult.UserAvatarUrl = tArr[tArr.GetArrayLength() - 1].GetProperty("url").GetString();
                    }
                }

                var homeShelf = new HomeShelf
                {
                    Title = !string.IsNullOrEmpty(title) ? title : "Recommendations",
                    Subtitle = strapline
                };

                if (shelf.TryGetProperty("contents", out var contents) && contents.GetArrayLength() > 0)
                {
                    foreach (var item in contents.EnumerateArray())
                    {
                        var track = ExtractSingleTrack(item, ref gradIndex);
                        if (track != null)
                        {
                            homeShelf.Items.Add(track);
                        }
                    }
                }

                if (homeShelf.Items.Count > 0)
                {
                    shelves.Add(homeShelf);
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                ExtractShelvesRecursive(prop.Value, shelves, ref gradIndex, feedResult);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                ExtractShelvesRecursive(child, shelves, ref gradIndex, feedResult);
            }
        }
    }

    private static SearchResult? ExtractSingleTrack(JsonElement item, ref int gradIndex)
    {
        string videoId = string.Empty;
        string title = string.Empty;
        string artist = string.Empty;
        string thumb = string.Empty;

        // Type A: musicResponsiveListItemRenderer
        if (item.TryGetProperty("musicResponsiveListItemRenderer", out var resp))
        {
            if (resp.TryGetProperty("playlistItemData", out var pData) && pData.TryGetProperty("videoId", out var vidProp))
            {
                videoId = vidProp.GetString() ?? string.Empty;
            }

            if (resp.TryGetProperty("flexColumns", out var cols) && cols.GetArrayLength() > 0)
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

            if (resp.TryGetProperty("thumbnail", out var thumbObj) &&
                thumbObj.TryGetProperty("musicThumbnailRenderer", out var mThumb) &&
                mThumb.TryGetProperty("thumbnail", out var innerThumb) &&
                innerThumb.TryGetProperty("thumbnails", out var tList) && tList.GetArrayLength() > 0)
            {
                thumb = tList[tList.GetArrayLength() - 1].GetProperty("url").GetString() ?? string.Empty;
            }
        }
        // Type B: musicTwoRowItemRenderer
        else if (item.TryGetProperty("musicTwoRowItemRenderer", out var twoRow))
        {
            if (twoRow.TryGetProperty("navigationEndpoint", out var nav) &&
                nav.TryGetProperty("watchEndpoint", out var watch) &&
                watch.TryGetProperty("videoId", out var wVid))
            {
                videoId = wVid.GetString() ?? string.Empty;
            }

            if (twoRow.TryGetProperty("title", out var tObj) &&
                tObj.TryGetProperty("runs", out var runs) && runs.GetArrayLength() > 0)
            {
                title = runs[0].GetProperty("text").GetString() ?? string.Empty;
            }

            if (twoRow.TryGetProperty("subtitle", out var sObj) &&
                sObj.TryGetProperty("runs", out var sRuns) && sRuns.GetArrayLength() > 0)
            {
                artist = sRuns[0].GetProperty("text").GetString() ?? string.Empty;
            }

            if (twoRow.TryGetProperty("thumbnailRenderer", out var tr) &&
                tr.TryGetProperty("musicThumbnailRenderer", out var mt) &&
                mt.TryGetProperty("thumbnail", out var th) &&
                th.TryGetProperty("thumbnails", out var tArr) && tArr.GetArrayLength() > 0)
            {
                thumb = tArr[tArr.GetArrayLength() - 1].GetProperty("url").GetString() ?? string.Empty;
            }

            // Extract videoId from thumbnail overlay play button if not found in navigationEndpoint
            if (string.IsNullOrEmpty(videoId) && twoRow.TryGetProperty("thumbnailOverlay", out var thumbOver) &&
                thumbOver.TryGetProperty("musicItemThumbnailOverlayRenderer", out var overlayRend) &&
                overlayRend.TryGetProperty("content", out var oContent) &&
                oContent.TryGetProperty("musicPlayButtonRenderer", out var pBtn) &&
                pBtn.TryGetProperty("playNavigationEndpoint", out var playNav) &&
                playNav.TryGetProperty("watchEndpoint", out var pWatch) &&
                pWatch.TryGetProperty("videoId", out var pVid))
            {
                videoId = pVid.GetString() ?? string.Empty;
            }

            // Extract videoId from onTap endpoint
            if (string.IsNullOrEmpty(videoId) && twoRow.TryGetProperty("onTap", out var onTap) &&
                onTap.TryGetProperty("watchEndpoint", out var otWatch) &&
                otWatch.TryGetProperty("videoId", out var otVid))
            {
                videoId = otVid.GetString() ?? string.Empty;
            }
        }

        if (!string.IsNullOrEmpty(title) && (!string.IsNullOrEmpty(videoId) || !string.IsNullOrEmpty(artist)))
        {
            if (string.IsNullOrEmpty(videoId))
            {
                videoId = $"query:{title}|||{artist}";
            }

            var (start, end) = AmbientGradients[gradIndex % AmbientGradients.Length];
            gradIndex++;

            return new SearchResult
            {
                VideoId = videoId,
                Title = title,
                Artist = !string.IsNullOrEmpty(artist) ? artist : "YouTube Music",
                ThumbnailUrl = thumb,
                Duration = TimeSpan.FromMinutes(3.5),
                GradientStart = start,
                GradientEnd = end,
                AudioQuality = "1411 kbps · FLAC · Lossless",
                FidelityTier = "Hi-Res Lossless"
            };
        }

        return null;
    }

    private static void PopulateDefaultFeed(HomeFeedResult result)
    {
        var defaultChips = new[]
        {
            ("All (सब)", ""),
            ("कसरत (Workout)", "Workout songs"),
            ("फ़ील गुड (Feel Good)", "Feel good songs"),
            ("रोमांस (Romance)", "Romantic songs"),
            ("सुकून (Relax)", "Relaxing acoustic songs"),
            ("पार्टी (Party)", "Party dance songs"),
            ("फ़ोकस (Focus)", "Lofi focus study music"),
            ("सफ़र (Commute)", "Travel roadtrip songs"),
            ("जोश (Energy)", "High energy beats"),
            ("दुख भरा (Sad)", "Sad soulful songs")
        };

        foreach (var (title, param) in defaultChips)
        {
            result.Chips.Add(new MoodChip { Title = title, Param = param });
        }
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

    public async Task<IReadOnlyList<Playlist>> GetLibraryPlaylistsAsync(CancellationToken ct = default)
    {
        var playlists = new List<Playlist>();
        try
        {
            string? cookies = null;
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                cookies = localSettings.Values["GoogleAuthCookies"] as string;
            }
            catch { }

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
                browseId = "FEmusic_library_landing"
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(reqObj), System.Text.Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/browse?prettyPrint=false")
            {
                Content = jsonContent
            };
            msg.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            AddAuthorizationHeaders(msg, cookies);

            using var resp = await _httpClient.SendAsync(msg, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                ParsePlaylistsRecursive(doc.RootElement, playlists);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("GetLibraryPlaylistsAsync failed", ex);
        }

        // Add auto Liked Songs playlist if not present
        if (!playlists.Any(p => p.Id == "VLLM" || p.Title.Contains("पसंद") || p.Title.Contains("Liked")))
        {
            playlists.Insert(0, new Playlist
            {
                Id = "VLLM",
                Title = "पसंद किया गया संगीत (Liked Music)",
                Description = "Auto Playlist · YouTube Music",
                ThumbnailUrl = "https://www.gstatic.com/youtube/media/ytm/images/pntiles/maps_liked_songs.png",
                TrackCount = 50
            });
        }

        return playlists;
    }

    public async Task<IReadOnlyList<Song>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
    {
        var songs = new List<Song>();
        try
        {
            string? cookies = null;
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                cookies = localSettings.Values["GoogleAuthCookies"] as string;
            }
            catch { }

            string browseId = playlistId.StartsWith("VL") ? playlistId : $"VL{playlistId}";

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
                browseId = browseId
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(reqObj), System.Text.Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "https://music.youtube.com/youtubei/v1/browse?prettyPrint=false")
            {
                Content = jsonContent
            };
            msg.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            AddAuthorizationHeaders(msg, cookies);

            using var resp = await _httpClient.SendAsync(msg, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                var browseTracks = ParseInnertubeBrowseTracks(json);
                foreach (var r in browseTracks)
                {
                    songs.Add(new Song
                    {
                        VideoId = r.VideoId,
                        Title = r.Title,
                        Artist = r.Artist,
                        ThumbnailUrl = r.ThumbnailUrl,
                        Duration = r.Duration,
                        AudioQuality = "1411 kbps · FLAC · Lossless",
                        AudioCodec = "FLAC",
                        FidelityTier = "Hi-Res Lossless",
                        AudioSource = "YouTube Music"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("GetPlaylistTracksAsync failed", ex);
        }

        return songs;
    }

    private static void ParsePlaylistsRecursive(JsonElement element, List<Playlist> playlists)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("musicTwoRowItemRenderer", out var twoRow))
            {
                string id = string.Empty;
                string title = string.Empty;
                string subtitle = string.Empty;
                string thumb = string.Empty;

                if (twoRow.TryGetProperty("navigationEndpoint", out var nav) &&
                    nav.TryGetProperty("browseEndpoint", out var bEnd) &&
                    bEnd.TryGetProperty("browseId", out var bId))
                {
                    id = bId.GetString() ?? string.Empty;
                }

                if (twoRow.TryGetProperty("title", out var tObj) &&
                    tObj.TryGetProperty("runs", out var runs) && runs.GetArrayLength() > 0)
                {
                    title = runs[0].GetProperty("text").GetString() ?? string.Empty;
                }

                if (twoRow.TryGetProperty("subtitle", out var sObj) &&
                    sObj.TryGetProperty("runs", out var sRuns) && sRuns.GetArrayLength() > 0)
                {
                    subtitle = sRuns[0].GetProperty("text").GetString() ?? string.Empty;
                }

                if (twoRow.TryGetProperty("thumbnailRenderer", out var tr) &&
                    tr.TryGetProperty("musicThumbnailRenderer", out var mt) &&
                    mt.TryGetProperty("thumbnail", out var th) &&
                    th.TryGetProperty("thumbnails", out var tArr) && tArr.GetArrayLength() > 0)
                {
                    thumb = tArr[tArr.GetArrayLength() - 1].GetProperty("url").GetString() ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(title) && !playlists.Any(p => p.Id == id))
                {
                    playlists.Add(new Playlist
                    {
                        Id = id,
                        Title = title,
                        Description = subtitle,
                        ThumbnailUrl = thumb,
                        TrackCount = 20
                    });
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                ParsePlaylistsRecursive(prop.Value, playlists);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                ParsePlaylistsRecursive(child, playlists);
            }
        }
    }
}
