using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using YTMusicClient.Models;

namespace YTMusicClient.Services;

public class LyricsService : ILyricsService
{
    private readonly HttpClient _httpClient;
    private const string LrcLibBaseUrl = "https://lrclib.net/api";

    public LyricsService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YTMusicClient/1.0");
    }

    public async Task<IReadOnlyList<LyricLine>> GetSyncedLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        try
        {
            // Try exact match first
            var url = $"{LrcLibBaseUrl}/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}&duration={Math.Round(duration.TotalSeconds)}";
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

            // Fallback: search
            var searchUrl = $"{LrcLibBaseUrl}/search?q={Uri.EscapeDataString($"{title} {artist}")}";
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
            Debug.WriteLine($"Lyrics fetch failed: {ex.Message}");
        }

        return Array.Empty<LyricLine>();
    }

    private static IReadOnlyList<LyricLine> ParseLrc(string lrc)
    {
        var lines = new List<LyricLine>();
        var regex = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");

        foreach (var line in lrc.Split('\n'))
        {
            var match = regex.Match(line);
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
        var lines = new List<LyricLine>();
        var textLines = plain.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        for (int i = 0; i < textLines.Length; i++)
        {
            lines.Add(new LyricLine
            {
                Timestamp = TimeSpan.FromSeconds(i * 5), // Rough estimate spacing
                Text = textLines[i].Trim()
            });
        }

        return lines;
    }

    private class LrcLibResponse
    {
        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }

        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }
    }
}
