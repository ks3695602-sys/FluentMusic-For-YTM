using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace YTMusicClient.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private ElementTheme _currentTheme = ElementTheme.Default;

    [ObservableProperty]
    private string _appVersion = "1.0.1";

    [ObservableProperty]
    private bool _isCompactMode;

    [ObservableProperty]
    private int _selectedQualityIndex;

    public string[] AudioQualities { get; } = { "Auto", "High (256kbps)", "Medium (128kbps)", "Low (64kbps)" };

    [RelayCommand]
    private void SetTheme(string theme)
    {
        CurrentTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }
}
