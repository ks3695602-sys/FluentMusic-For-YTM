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
    private const string BetterLyricsBaseUrl = "https://api.betterlyrics.org";
    private const string YouLyPlusBaseUrl = "https://lyricsplus.prjktla.my.id/v2/lyrics/get";
    private const string LrcLibBaseUrl = "https://lrclib.net/api";

    public LyricsService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) FluentMusic/1.0.3");
    }

    public async Task<IReadOnlyList<LyricLine>> GetSyncedLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        var cleanTitle = CleanSongTitle(title);
        var cleanArtist = CleanArtistName(artist);
        var durationSec = (int)Math.Round(duration.TotalSeconds);

        // 1. Primary Engine: Better Lyrics API (Word-by-Word TTML)
        try
        {
            AppLogger.Log($"LyricsService: Querying Better Lyrics for '{cleanTitle}' - '{cleanArtist}'");
            var betterLyricsUrl = $"{BetterLyricsBaseUrl}/getLyrics?s={Uri.EscapeDataString(cleanTitle)}&a={Uri.EscapeDataString(cleanArtist)}&d={durationSec}";
            var response = await _httpClient.GetAsync(betterLyricsUrl, ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ttml", out var ttmlProp))
                {
                    var ttml = ttmlProp.GetString();
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
        }
        catch (Exception ex)
        {
            AppLogger.Log($"Better Lyrics fetch failed: {ex.Message}");
        }

        // 2. Secondary Engine: YouLy+ / LyricsPlus API (Multi-Source Apple/Spotify/Musixmatch)
        try
        {
            AppLogger.Log($"LyricsService: Querying YouLy+ for '{cleanTitle}' - '{cleanArtist}'");
            var youLyUrl = $"{YouLyPlusBaseUrl}?title={Uri.EscapeDataString(cleanTitle)}&artist={Uri.EscapeDataString(cleanArtist)}&duration={durationSec}&source=apple,musixmatch,spotify";
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
            AppLogger.Log($"YouLy+ fetch failed: {ex.Message}");
        }

        // 3. Tertiary Engine: LRCLIB API Fallback (Line-Synced)
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
                                Text = wordText,
                                StartTime = wordBegin,
                                EndTime = wordEnd
                            });
                        }
                    }
                }

                var fullText = spanElements.Count > 0
                    ? string.Join("", words.Select(w => w.Text)).Trim()
                    : p.Value.Trim();

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
