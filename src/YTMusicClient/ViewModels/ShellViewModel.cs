using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IMediaPlaybackService _playbackService;

    [ObservableProperty]
    private string _selectedNavTag = "home";

    [ObservableProperty]
    private bool _isLyricsPanelOpen;

    public PlayerBarViewModel PlayerBar { get; }
    public LyricsViewModel Lyrics { get; }

    public ShellViewModel(
        INavigationService navigationService,
        IMediaPlaybackService playbackService,
        PlayerBarViewModel playerBar,
        LyricsViewModel lyrics)
    {
        _navigationService = navigationService;
        _playbackService = playbackService;
        PlayerBar = playerBar;
        Lyrics = lyrics;
    }

    [RelayCommand]
    private void ToggleLyricsPanel()
    {
        IsLyricsPanelOpen = !IsLyricsPanelOpen;
    }
}
