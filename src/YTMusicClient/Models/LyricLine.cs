using CommunityToolkit.Mvvm.ComponentModel;

namespace YTMusicClient.Models;

public partial class LyricLine : ObservableObject
{
    public TimeSpan Timestamp { get; set; }
    public string Text { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isActive;
}
