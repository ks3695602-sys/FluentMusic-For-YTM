using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YTMusicClient.Models;

public partial class LyricWord : ObservableObject
{
    public string Text { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    [ObservableProperty]
    private bool _isActive;
}

public partial class LyricLine : ObservableObject
{
    public TimeSpan Timestamp { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Text { get; set; } = string.Empty;

    public List<LyricWord> Words { get; set; } = new();
    public bool HasWords => Words.Count > 0;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _activeWordsText = string.Empty;

    [ObservableProperty]
    private string _remainingWordsText = string.Empty;
}
