using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using YTMusicClient.Helpers;
using YTMusicClient.Services;
using YTMusicClient.ViewModels;
using YTMusicClient.Views;

namespace YTMusicClient;

public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    public MainWindow()
    {
        this.InitializeComponent();

        ViewModel = App.Services.GetRequiredService<ShellViewModel>();

        // Zero-titlebar edge-to-edge window shell with draggable sidebar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(SidebarDragArea);

        // Make window caption buttons transparent so they seamlessly blend
        ConfigureTransparentCaptionButtons();

        // Configure authentic Windows 11 Mica Alt backdrop
        TrySetBackdrop();

        // Window size & position
        WindowHelper.SetWindowSize(this, 1280, 820);
        WindowHelper.CenterOnScreen(this);

        // Register navigation frame
        var navService = App.Services.GetRequiredService<INavigationService>();
        navService.Frame = ContentFrame;

        // Wire Floating PlayerBar lyrics and now-playing events
        PlayerBarControl.LyricsToggled += (s, e) =>
        {
            LyricsSplitView.IsPaneOpen = !LyricsSplitView.IsPaneOpen;
        };

        PlayerBarControl.NowPlayingRequested += (s, e) =>
        {
            ToggleNowPlaying();
        };

        this.Activated += (s, e) => CheckGoogleSignInStatus();

        // Navigate to Home by default
        ContentFrame.Navigate(typeof(HomePage));
        SetActiveNavButton(0);
        CheckGoogleSignInStatus();
    }

    public void CheckGoogleSignInStatus()
    {
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            bool isSignedIn = localSettings.Values["IsGoogleSignedIn"] is bool b && b;
            if (isSignedIn)
            {
                var userName = localSettings.Values["UserDisplayName"] as string ?? "Google User";
                UserAccountStatusTitle.Text = userName;
                UserAccountStatusSub.Text = "Connected & Synced";
                UserAccountAvatarIcon.Glyph = "\uE73E";
                UserAccountAvatarIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129));
            }
            else
            {
                UserAccountStatusTitle.Text = "Google Sync";
                UserAccountStatusSub.Text = "Sign in to sync";
                UserAccountAvatarIcon.Glyph = "\uE77B";
                UserAccountAvatarIcon.Foreground = (Brush)Application.Current.Resources["AccentRedBrush"];
            }
        }
        catch { }
    }

    private void ToggleNowPlaying()
    {
        bool isShowing = NowPlayingOverlay.Visibility == Visibility.Visible;
        if (isShowing)
        {
            NowPlayingOverlay.Visibility = Visibility.Collapsed;
            PauseVideoPlayer();
        }
        else
        {
            NowPlayingOverlay.Visibility = Visibility.Visible;
            if (NpVideoStage.Visibility == Visibility.Visible)
            {
                LoadCurrentVideo();
            }
        }
    }

    private void CloseNowPlaying_Click(object sender, RoutedEventArgs e)
    {
        NowPlayingOverlay.Visibility = Visibility.Collapsed;
        PauseVideoPlayer();
    }

    private void NpAudioMode_Click(object sender, RoutedEventArgs e)
    {
        NpAudioStage.Visibility = Visibility.Visible;
        NpVideoStage.Visibility = Visibility.Collapsed;
        NpAudioModeBtn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        NpVideoModeBtn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        PauseVideoPlayer();
    }

    private void NpVideoMode_Click(object sender, RoutedEventArgs e)
    {
        NpAudioStage.Visibility = Visibility.Collapsed;
        NpVideoStage.Visibility = Visibility.Visible;
        NpVideoModeBtn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        NpAudioModeBtn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        LoadCurrentVideo();
    }

    private void LoadCurrentVideo()
    {
        var currentSong = ViewModel.PlaybackService.CurrentSong;
        if (currentSong != null && !string.IsNullOrEmpty(currentSong.VideoId))
        {
            try
            {
                NpVideoWebView.Source = new Uri($"https://www.youtube-nocookie.com/embed/{currentSong.VideoId}?autoplay=1");
            }
            catch { }
        }
    }

    private void PauseVideoPlayer()
    {
        try
        {
            NpVideoWebView.Source = new Uri("about:blank");
        }
        catch { }
    }

    private void ConfigureTransparentCaptionButtons()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow?.TitleBar != null)
            {
                appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(40, 255, 255, 255);
                appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(60, 255, 255, 255);
            }
        }
        catch { }
    }

    private void TrySetBackdrop()
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            this.SystemBackdrop = new MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            };
        }
        else if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }

    private void SetActiveNavButton(int index)
    {
        var activeBg = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        var inactiveBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        NavHomeBtn.Background = index == 0 ? activeBg : inactiveBg;
        NavExploreBtn.Background = index == 1 ? activeBg : inactiveBg;
        NavLibraryBtn.Background = index == 2 ? activeBg : inactiveBg;
        NavSettingsBtn.Background = index == 3 ? activeBg : inactiveBg;
    }

    private void NavHome_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HomePage));
        SetActiveNavButton(0);
    }

    private void NavExplore_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ExplorePage));
        SetActiveNavButton(1);
    }

    private void NavLibrary_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(LibraryPage));
        SetActiveNavButton(2);
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        SetActiveNavButton(3);
    }

    private void UserAccountPill_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        SetActiveNavButton(3);
    }
}
