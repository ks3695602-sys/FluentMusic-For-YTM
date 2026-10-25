using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using YTMusicClient.Helpers;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public class LyricsService : ILyricsService
{
    private readonly HttpClient _httpClient;
    private static readonly string[] BetterLyricsEndpoints = new[]
    {
        "https://lyrics-api.boidu.dev/getLyrics",
        "https://api.betterlyrics.org/getLyrics"
    };
    private static readonly string[] YouLyPlusEndpoints = new[]
    {
        "https://lyricsplus.binimum.org/v2/lyrics/get",
        "https://lyricsplus.prjktla.my.id/v2/lyrics/get",
        "https://lyricsplus.atomix.one/v2/lyrics/get"
    };
    private const string LrcLibBaseUrl = "https://lrclib.net/api";
    private const string SimpMusicBaseUrl = "https://api-lyrics.simpmusic.org/v1";

    public LyricsService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ArchiveTune/1.0 (Windows NT 10.0; Win64; x64) FluentMusic/1.1.1");
    }

    public async Task<IReadOnlyList<LyricLine>> GetSyncedLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        string? videoId = null,
        CancellationToken ct = default)
    {
        var cleanTitle = CleanSongTitle(title);
        var cleanArtist = CleanArtistName(artist);
        var durationSec = (int)Math.Round(duration.TotalSeconds);

        // 1. SimpMusic API (Direct YouTube VideoId resolution from ArchiveTune)
        if (!string.IsNullOrEmpty(videoId))
        {
            try
            {
                AppLogger.Log($"LyricsService: Querying SimpMusic for videoId '{videoId}'");
                var simpLines = await FetchSimpMusicLyricsAsync(videoId, durationSec, ct);
                if (simpLines.Count > 0)
                {
                    AppLogger.Log($"SimpMusic SUCCESS: {simpLines.Count} lines parsed");
                    return simpLines;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"SimpMusic fetch failed: {ex.Message}");
            }
        }

        // 2. Primary Engine: Better Lyrics API (Word-by-Word TTML from ArchiveTune)
        foreach (var betterLyricsUrlBase in BetterLyricsEndpoints)
        {
            try
            {
                AppLogger.Log($"LyricsService: Querying Better Lyrics at {betterLyricsUrlBase} for '{cleanTitle}' - '{cleanArtist}'");
                var betterLyricsUrl = $"{betterLyricsUrlBase}?s={Uri.EscapeDataString(cleanTitle)}&a={Uri.EscapeDataString(cleanArtist)}&d={durationSec}";
                var response = await _httpClient.GetAsync(betterLyricsUrl, ct);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    string? ttml = null;
                    if (doc.RootElement.TryGetProperty("ttml", out var ttmlProp))
                    {
                        ttml = ttmlProp.GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("lyrics", out var lyrProp))
                    {
                        ttml = lyrProp.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(ttml))
                    {
                        var parsed = ParseTtml(ttml);
                        if (parsed.Count > 0)
                        {
                            AppLogger.Log($"Better Lyrics SUCCESS: {parsed.Count} lines (Word-synced TTML)");
                            return parsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"Better Lyrics fetch failed from {betterLyricsUrlBase}: {ex.Message}");
            }
        }

        // 3. Secondary Engine: YouLy+ / LyricsPlus API (Multi-Source Apple/Spotify/Musixmatch from ArchiveTune)
        foreach (var youLyBaseUrl in YouLyPlusEndpoints)
        {
            try
            {
                AppLogger.Log($"LyricsService: Querying YouLy+ at {youLyBaseUrl} for '{cleanTitle}' - '{cleanArtist}'");
                var youLyUrl = $"{youLyBaseUrl}?title={Uri.EscapeDataString(cleanTitle)}&artist={Uri.EscapeDataString(cleanArtist)}&duration={durationSec}&source=apple,musixmatch,spotify";
                var response = await _httpClient.GetAsync(youLyUrl, ct);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var parsed = ParseYouLyPlusJson(json);
                    if (parsed.Count > 0)
                    {
                        AppLogger.Log($"YouLy+ SUCCESS: {parsed.Count} lines parsed");
                        return parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"YouLy+ fetch failed from {youLyBaseUrl}: {ex.Message}");
            }
        }

        // 4. KuGou Music Lyrics Engine (Direct from ArchiveTune)
        try
        {
            AppLogger.Log($"LyricsService: Querying KuGou for '{cleanTitle}' - '{cleanArtist}'");
            var kugouLines = await FetchKuGouLyricsAsync(cleanTitle, cleanArtist, durationSec, ct);
            if (kugouLines.Count > 0)
            {
                AppLogger.Log($"KuGou SUCCESS: {kugouLines.Count} lines parsed");
                return kugouLines;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"KuGou fetch failed: {ex.Message}");
        }

        // 4. Quaternary Engine: LRCLIB API Fallback (Line-Synced)
        try
        {
            AppLogger.Log($"LyricsService: Fallback to LRCLIB for '{cleanTitle}' - '{cleanArtist}'");
            var url = $"{LrcLibBaseUrl}/get?track_name={Uri.EscapeDataString(cleanTitle)}&artist_name={Uri.EscapeDataString(cleanArtist)}&duration={durationSec}";
            var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var lrcResult = await response.Content.ReadFromJsonAsync<LrcLibResponse>(cancellationToken: ct);
                if (!string.IsNullOrEmpty(lrcResult?.SyncedLyrics))
                {
                    return ParseLrc(lrcResult.SyncedLyrics);
                }
                if (!string.IsNullOrEmpty(lrcResult?.PlainLyrics))
                {
                    return ParsePlainLyrics(lrcResult.PlainLyrics);
                }
            }

            // LRCLIB Search fallback
            var searchUrl = $"{LrcLibBaseUrl}/search?q={Uri.EscapeDataString($"{cleanTitle} {cleanArtist}")}";
            var searchResponse = await _httpClient.GetAsync(searchUrl, ct);

            if (searchResponse.IsSuccessStatusCode)
            {
                var searchResults = await searchResponse.Content.ReadFromJsonAsync<List<LrcLibResponse>>(cancellationToken: ct);
                var best = searchResults?.FirstOrDefault(r => !string.IsNullOrEmpty(r.SyncedLyrics));
                if (best != null)
                {
                    return ParseLrc(best.SyncedLyrics!);
                }
                var plainBest = searchResults?.FirstOrDefault(r => !string.IsNullOrEmpty(r.PlainLyrics));
                if (plainBest != null)
                {
                    return ParsePlainLyrics(plainBest.PlainLyrics!);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LRCLIB fetch failed: {ex.Message}");
        }

        return Array.Empty<LyricLine>();
    }

    private static IReadOnlyList<LyricLine> ParseTtml(string ttmlXml)
    {
        var result = new List<LyricLine>();
        try
        {
            var doc = XDocument.Parse(ttmlXml);
            // Search all elements named "p" regardless of XML namespace
            var pElements = doc.Descendants().Where(e => e.Name.LocalName.Equals("p", StringComparison.OrdinalIgnoreCase));

            foreach (var p in pElements)
            {
                var beginAttr = p.Attribute("begin")?.Value;
                var endAttr = p.Attribute("end")?.Value;

                var lineStartTime = ParseTime(beginAttr);
                var lineEndTime = ParseTime(endAttr);

                var spanElements = p.Elements().Where(e => e.Name.LocalName.Equals("span", StringComparison.OrdinalIgnoreCase)).ToList();
                var words = new List<LyricWord>();

                if (spanElements.Count > 0)
                {
                    foreach (var span in spanElements)
                    {
                        var wordBegin = ParseTime(span.Attribute("begin")?.Value);
                        var wordEnd = ParseTime(span.Attribute("end")?.Value);
                        var wordText = span.Value;

                        if (!string.IsNullOrEmpty(wordText))
                        {
                            words.Add(new LyricWord
                            {
                                Text = wordText.Trim(),
                                StartTime = wordBegin,
                                EndTime = wordEnd
                            });
                        }
                    }
                }

                var fullText = !string.IsNullOrWhiteSpace(p.Value)
                    ? p.Value.Trim()
                    : string.Join(" ", words.Select(w => w.Text)).Trim();

                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    result.Add(new LyricLine
                    {
                        Timestamp = lineStartTime,
                        EndTime = lineEndTime,
                        Text = fullText,
                        Words = words
                    });
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"ParseTtml failed: {ex.Message}");
        }

        return result;
    }

    private static IReadOnlyList<LyricLine> ParseYouLyPlusJson(string json)
    {
        var result = new List<LyricLine>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement linesElement;

            if (doc.RootElement.TryGetProperty("lines", out linesElement) ||
                (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("lines", out linesElement)))
            {
                foreach (var item in linesElement.EnumerateArray())
                {
                    var text = item.TryGetProperty("text", out var tProp) ? tProp.GetString() ?? string.Empty : string.Empty;
                    var startMs = item.TryGetProperty("startTimeMs", out var sProp) && sProp.TryGetInt64(out var s) ? s : 0;
                    var endMs = item.TryGetProperty("endTimeMs", out var eProp) && eProp.TryGetInt64(out var e) ? e : 0;

                    var words = new List<LyricWord>();
                    if (item.TryGetProperty("words", out var wordsArr) && wordsArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var w in wordsArr.EnumerateArray())
                        {
                            var wText = w.TryGetProperty("text", out var wt) ? wt.GetString() ?? string.Empty : string.Empty;
                            var wStart = w.TryGetProperty("startTimeMs", out var ws) && ws.TryGetInt64(out var wst) ? wst : 0;
                            var wEnd = w.TryGetProperty("endTimeMs", out var we) && we.TryGetInt64(out var wet) ? wet : 0;

                            if (!string.IsNullOrEmpty(wText))
                            {
                                words.Add(new LyricWord
                                {
                                    Text = wText,
                                    StartTime = TimeSpan.FromMilliseconds(wStart),
                                    EndTime = TimeSpan.FromMilliseconds(wEnd)
                                });
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(new LyricLine
                        {
                            Timestamp = TimeSpan.FromMilliseconds(startMs),
                            EndTime = TimeSpan.FromMilliseconds(endMs),
                            Text = text.Trim(),
                            Words = words
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"ParseYouLyPlusJson error: {ex.Message}");
        }

        return result;
    }

    private static TimeSpan ParseTime(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return TimeSpan.Zero;
        timeStr = timeStr.Trim();

        // Format: 00:15.200 or 01:23:45.678
        var parts = timeStr.Split(':');
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out var min) && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var sec))
            {
                return TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
            }
        }
        else if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out var hr) && int.TryParse(parts[1], out var min) && double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var sec))
            {
                return TimeSpan.FromHours(hr) + TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
            }
        }
        else if (timeStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            var raw = timeStr.TrimEnd('s', 'S');
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var sec))
            {
                return TimeSpan.FromSeconds(sec);
            }
        }
        else if (double.TryParse(timeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sec))
        {
            return TimeSpan.FromSeconds(sec);
        }

        return TimeSpan.Zero;
    }

    private static IReadOnlyList<LyricLine> ParseLrc(string lrc)
    {
        var lines = new List<LyricLine>();
        var regex = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");

        foreach (var rawLine in lrc.Split('\n'))
        {
            var match = regex.Match(rawLine);
            if (match.Success)
            {
                var minutes = int.Parse(match.Groups[1].Value);
                var seconds = int.Parse(match.Groups[2].Value);
                var centiseconds = match.Groups[3].Value;
                var milliseconds = centiseconds.Length == 2
                    ? int.Parse(centiseconds) * 10
                    : int.Parse(centiseconds);

                var text = match.Groups[4].Value.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(new LyricLine
                    {
                        Timestamp = new TimeSpan(0, 0, minutes, seconds, milliseconds),
                        Text = text
                    });
                }
            }
        }

        return lines.OrderBy(l => l.Timestamp).ToList();
    }

    private static IReadOnlyList<LyricLine> ParsePlainLyrics(string plain)
    {
        return plain.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select((text, index) => new LyricLine
            {
                Timestamp = TimeSpan.FromSeconds(index * 4),
                Text = text
            })
            .ToList();
    }

    private async Task<IReadOnlyList<LyricLine>> FetchSimpMusicLyricsAsync(string videoId, int durationSec, CancellationToken ct)
    {
        try
        {
            var url = $"{SimpMusicBaseUrl}/{Uri.EscapeDataString(videoId)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.Clear();
            req.Headers.UserAgent.ParseAdd("SimpMusicLyrics/1.0");

            var resp = await _httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<LyricLine>();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array && dataArr.GetArrayLength() > 0)
            {
                JsonElement bestTrack = default;
                int bestDiff = int.MaxValue;

                foreach (var track in dataArr.EnumerateArray())
                {
                    if (track.TryGetProperty("syncedLyrics", out var sProp) && !string.IsNullOrWhiteSpace(sProp.GetString()))
                    {
                        int dur = track.TryGetProperty("duration", out var dProp) && dProp.TryGetInt32(out var d) ? d : 0;
                        int diff = durationSec > 0 ? Math.Abs(dur - durationSec) : 0;
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestTrack = track;
                        }
                    }
                }

                if (bestTrack.ValueKind != JsonValueKind.Undefined)
                {
                    if (bestTrack.TryGetProperty("syncedLyrics", out var syn) && !string.IsNullOrWhiteSpace(syn.GetString()))
                    {
                        return ParseLrc(syn.GetString()!);
                    }
                    if (bestTrack.TryGetProperty("plainLyrics", out var pln) && !string.IsNullOrWhiteSpace(pln.GetString()))
                    {
                        return ParsePlainLyrics(pln.GetString()!);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"FetchSimpMusicLyricsAsync error: {ex.Message}");
        }

        return Array.Empty<LyricLine>();
    }

    private async Task<IReadOnlyList<LyricLine>> FetchKuGouLyricsAsync(string title, string artist, int durationSec, CancellationToken ct)
    {
        try
        {
            var searchUrl = $"https://lyrics.kugou.com/search?ver=1&man=yes&client=pc&keyword={Uri.EscapeDataString($"{title} - {artist}")}&duration={durationSec * 1000}";
            var resp = await _httpClient.GetAsync(searchUrl, ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<LyricLine>();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];
                if (first.TryGetProperty("id", out var idProp) && first.TryGetProperty("accesskey", out var keyProp))
                {
                    var id = idProp.GetInt64();
                    var key = keyProp.GetString();
                    if (!string.IsNullOrEmpty(key))
                    {
                        var downloadUrl = $"https://lyrics.kugou.com/download?fmt=lrc&charset=utf8&client=pc&ver=1&id={id}&accesskey={key}";
                        var downResp = await _httpClient.GetAsync(downloadUrl, ct);
                        if (downResp.IsSuccessStatusCode)
                        {
                            var downJson = await downResp.Content.ReadAsStringAsync(ct);
                            using var downDoc = JsonDocument.Parse(downJson);
                            if (downDoc.RootElement.TryGetProperty("content", out var contentProp))
                            {
                                var b64 = contentProp.GetString();
                                if (!string.IsNullOrEmpty(b64))
                                {
                                    var lrcBytes = Convert.FromBase64String(b64);
                                    var lrcText = System.Text.Encoding.UTF8.GetString(lrcBytes);
                                    return ParseLrc(lrcText);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"FetchKuGouLyricsAsync error: {ex.Message}");
        }

        return Array.Empty<LyricLine>();
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _translationCache = new();

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var cacheKey = $"{targetLanguage}_{text}";
        if (_translationCache.TryGetValue(cacheKey, out var cached)) return cached;

        bool isHinglish = targetLanguage.Equals("hinglish", StringComparison.OrdinalIgnoreCase) ||
                          targetLanguage.Equals("hi-latn", StringComparison.OrdinalIgnoreCase);
        string langCode = isHinglish ? "hi" : targetLanguage;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://translate.googleapis.com/translate_a/single?client=at&dt=t&ie=UTF-8&oe=UTF-8&sl=auto&tl={langCode}&q={Uri.EscapeDataString(text)}");
            req.Headers.UserAgent.Clear();
            req.Headers.UserAgent.ParseAdd("ArchiveTune Android");

            var resp = await _httpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var segs = doc.RootElement[0];
                    var sb = new System.Text.StringBuilder();
                    foreach (var seg in segs.EnumerateArray())
                    {
                        if (seg.GetArrayLength() > 0)
                        {
                            sb.Append(seg[0].GetString());
                        }
                    }
                    var translated = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(translated))
                    {
                        if (isHinglish)
                        {
                            translated = TransliterateDevanagariToLatin(translated);
                        }
                        _translationCache[cacheKey] = translated;
                        return translated;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"Lyrics Translation error: {ex.Message}");
        }

        return text;
    }

    public static string TransliterateDevanagariToLatin(string devanagariText)
    {
        if (string.IsNullOrEmpty(devanagariText)) return devanagariText;

        var map = new Dictionary<string, string>
        {
            ["क"] = "ka", ["ख"] = "kha", ["ग"] = "ga", ["घ"] = "gha", ["ङ"] = "nga",
            ["च"] = "cha", ["छ"] = "chha", ["ज"] = "ja", ["झ"] = "jha", ["ञ"] = "nya",
            ["ट"] = "ta", ["ठ"] = "tha", ["ड"] = "da", ["ढ"] = "dha", ["ण"] = "na",
            ["त"] = "ta", ["थ"] = "tha", ["द"] = "da", ["ध"] = "dha", ["न"] = "na",
            ["प"] = "pa", ["फ"] = "pha", ["ब"] = "ba", ["भ"] = "bha", ["म"] = "ma",
            ["य"] = "ya", ["र"] = "ra", ["ल"] = "la", ["व"] = "va",
            ["श"] = "sha", ["ष"] = "sha", ["स"] = "sa", ["ह"] = "ha",
            ["ा"] = "a", ["ि"] = "i", ["ी"] = "ee", ["ु"] = "u", ["ू"] = "oo",
            ["ृ"] = "ri", ["े"] = "e", ["ै"] = "ai", ["ो"] = "o", ["ौ"] = "au",
            ["ं"] = "n", ["ँ"] = "n", ["ः"] = "h", ["्"] = "",
            ["अ"] = "a", ["आ"] = "aa", ["इ"] = "i", ["ई"] = "ee", ["उ"] = "u", ["ऊ"] = "oo",
            ["ऋ"] = "ri", ["ए"] = "e", ["ऐ"] = "ai", ["ओ"] = "o", ["औ"] = "au"
        };

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < devanagariText.Length; i++)
        {
            string ch = devanagariText[i].ToString();
            // check for matra following consonant
            if (i + 1 < devanagariText.Length && map.ContainsKey(devanagariText[i + 1].ToString()) && "ािीुूृेैोौ्".Contains(devanagariText[i + 1]))
            {
                if (map.TryGetValue(ch, out var cons))
                {
                    // drop the trailing inherent 'a'
                    if (cons.EndsWith("a")) cons = cons.Substring(0, cons.Length - 1);
                    var matra = map[devanagariText[i + 1].ToString()];
                    sb.Append(cons).Append(matra);
                    i++;
                    continue;
                }
            }

            if (map.TryGetValue(ch, out var roman))
            {
                sb.Append(roman);
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string CleanSongTitle(string title)
    {
        // Strip official video, ft, etc. for better lyric matching
        var cleaned = Regex.Replace(title, @"\((feat\.|ft\.|official|video|audio|remix|lyric|lyrics).*\)", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[(feat\.|ft\.|official|video|audio|remix|lyric|lyrics).*\]", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private static string CleanArtistName(string artist)
    {
        var cleaned = Regex.Replace(artist, @"- Topic$", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"VEVO$", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private class LrcLibResponse
    {
        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }
    }
}
