using System;

namespace YTMusicClient.Models;

public class Song
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? StreamUrl { get; set; }

    // Audio Quality & Source Metadata
    public string AudioQuality { get; set; } = "256 kbps · AAC · Hi-Res";
    public string AudioCodec { get; set; } = "AAC";
    public string AudioSource { get; set; } = "BravePipe / Qobuz";

    // Dynamic Ambient Gradient Colors
    public string GradientColorStart { get; set; } = "#3B2028";
    public string GradientColorEnd { get; set; } = "#1B1C22";
}
