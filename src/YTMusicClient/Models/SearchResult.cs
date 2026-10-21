using System;

namespace YTMusicClient.Models;

public class SearchResult
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }

    // Dynamic Ambient Gradient & Subtitle
    public string GradientStart { get; set; } = "#262330";
    public string GradientEnd { get; set; } = "#181A20";
    public string Subtitle => string.IsNullOrEmpty(Album) ? $"Song · {Artist}" : $"{Artist} — {Album}";
    public string AudioQuality { get; set; } = "256 kbps · AAC · Hi-Res";
    public SearchResult Self => this;
}
