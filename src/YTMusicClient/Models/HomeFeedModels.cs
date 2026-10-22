using System;
using System.Collections.Generic;

namespace YTMusicClient.Models;

public class MoodChip
{
    public string Title { get; set; } = string.Empty;
    public string Param { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class HomeShelf
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string BrowseEndpoint { get; set; } = string.Empty;
    public List<SearchResult> Items { get; set; } = new();
    public bool HasItems => Items.Count > 0;
    public bool IsSongList => Items.Count > 0 && (Title.Contains("झटपट") || Title.Contains("Quick") || Title.Contains("पसंदीदा"));
}

public class HomeFeedResult
{
    public List<MoodChip> Chips { get; set; } = new();
    public List<HomeShelf> Shelves { get; set; } = new();
    public string? UserName { get; set; }
    public string? UserAvatarUrl { get; set; }
}
