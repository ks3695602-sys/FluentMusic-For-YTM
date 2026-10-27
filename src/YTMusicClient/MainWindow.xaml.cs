using System;
using Windows.Media.Playback;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using YTMusicClient.Helpers;
using YTMusicClient.Services;
using YTMusicClient.ViewModels;
using YTMusicClient.Views;

namespace YTMusicClient;

public sealed partial class MainWindow : Window
{
    public static MainWindow? Current { get; private set; }
    public ShellViewModel ViewModel { get; }
    private bool _isSidebarCollapsed = false;

    public MainWindow()
    {
        Current = this;
        this.InitializeComponent();

        ViewModel = App.Services.GetRequiredService<ShellViewModel>();

        // Zero-titlebar edge-to-edge window shell with draggable sidebar area
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(SidebarMidDragArea);

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

        // Ambient mesh animation for Apple Music Now Playing
        StartAmbientMeshAnimation();

        // Wire Localization dynamic updates
        LocalizationService.Instance.LanguageChanged += (s, e) => ApplyLocalization();
        ApplyLocalization();

        // Wire Floating PlayerBar lyrics and now-playing events
        PlayerBarControl.LyricsToggled += (s, e) =>
        {
            if (!LyricsSplitView.IsPaneOpen)
            {
                SwitchSidebarTab("lyrics");
                LyricsSplitView.IsPaneOpen = true;
            }
            else if (SidebarLyricsView.Visibility == Visibility.Visible)
            {
                LyricsSplitView.IsPaneOpen = false;
            }
            else
            {
                SwitchSidebarTab("lyrics");
            }
        };

        PlayerBarControl.NowPlayingSidebarToggled += (s, e) =>
        {
            if (!LyricsSplitView.IsPaneOpen)
            {
                SwitchSidebarTab("nowplaying");
                LyricsSplitView.IsPaneOpen = true;
            }
            else if (SidebarNowPlayingView.Visibility == Visibility.Visible)
            {
                LyricsSplitView.IsPaneOpen = false;
            }
            else
            {
                SwitchSidebarTab("nowplaying");
            }
        };

        PlayerBarControl.NowPlayingRequested += (s, e) =>
        {
            ToggleNowPlaying();
        };

        ViewModel.PlayerBar.PropertyChanged += (s, e) => UpdateNowPlayingContent();

        this.Activated += (s, e) => CheckGoogleSignInStatus();
        ContentFrame.Navigated += (s, e) => CheckGoogleSignInStatus();

        // Sync video player state with media playback service
        ViewModel.PlaybackService.PlaybackStateChanged += (s, state) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (NpVideoStage.Visibility == Visibility.Visible && _isVideoInitialized)
                {
                    if (state == MediaPlaybackState.Playing)
                    {
                        _ = NpVideoWebView.ExecuteScriptAsync("document.getElementById('ytplayer')?.contentWindow?.postMessage('{"event":"command","func":"playVideo","args":""}', '*');");
                    }
                    else if (state == MediaPlaybackState.Paused)
                    {
                        _ = NpVideoWebView.ExecuteScriptAsync("document.getElementById('ytplayer')?.contentWindow?.postMessage('{"event":"command","func":"pauseVideo","args":""}', '*');");
                    }
                }
            });
        };

        ViewModel.PlaybackService.PositionChanged += (s, pos) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (NowPlayingOverlay.Visibility == Visibility.Visible && ViewModel?.PlaybackService != null)
                {
                    var dur = ViewModel.PlaybackService.Duration;
                    NpPositionText.Text = FormatTime(pos);
                    NpDurationText.Text = FormatTime(dur);
                    if (dur.TotalSeconds > 0)
                    {
                        NpPositionSlider.Value = (pos.TotalSeconds / dur.TotalSeconds) * 100.0;
                    }
                }
            });
        };

        // Navigate to Home by default
        ContentFrame.Navigate(typeof(HomePage));
        SetActiveNavButton(0);
        CheckGoogleSignInStatus();
        UpdateNowPlayingContent();
    }

    public void CheckGoogleSignInStatus()
    {
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            bool isSignedIn = (localSettings.Values["IsGoogleSignedIn"] is bool b && b) ||
                              (localSettings.Values["GoogleAuthCookies"] is string cookies && !string.IsNullOrWhiteSpace(cookies));

            if (isSignedIn)
            {
                var userName = localSettings.Values["UserDisplayName"] as string;
                if (string.IsNullOrWhiteSpace(userName) || userName == "Google User")
                {
                    userName = "JPGRS_GAMING09";
                }
                var userAvatar = localSettings.Values["UserAvatarUrl"] as string;

                UserAccountStatusTitle.Text = userName;
                UserAccountStatusSub.Text = "Connected & Synced";

                if (!string.IsNullOrEmpty(userAvatar) && Uri.TryCreate(userAvatar, UriKind.Absolute, out var uri))
                {
                    UserAccountAvatarImage.Source = new BitmapImage(uri);
                    UserAccountAvatarImage.Visibility = Visibility.Visible;
                    UserAccountAvatarIcon.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UserAccountAvatarIcon.Glyph = "\uE73E";
                    UserAccountAvatarIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129));
                    UserAccountAvatarImage.Visibility = Visibility.Collapsed;
                    UserAccountAvatarIcon.Visibility = Visibility.Visible;
                }
            }
            else
            {
                UserAccountStatusTitle.Text = "Google Sync";
                UserAccountStatusSub.Text = "Sign in to sync";
                UserAccountAvatarIcon.Glyph = "\uE77B";
                UserAccountAvatarIcon.Foreground = (Brush)Application.Current.Resources["AccentRedBrush"];
                UserAccountAvatarImage.Visibility = Visibility.Collapsed;
                UserAccountAvatarIcon.Visibility = Visibility.Visible;
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
            ContentFrame.Visibility = Visibility.Visible;
            FloatingPlayerContainer.Visibility = Visibility.Visible;
            PauseVideoPlayer();
        }
        else
        {
            if (ViewModel?.PlaybackService?.CurrentSong == null && string.IsNullOrEmpty(ViewModel?.PlayerBar?.Title))
            {
                return;
            }
            ContentFrame.Visibility = Visibility.Collapsed;
            FloatingPlayerContainer.Visibility = Visibility.Collapsed;
            NowPlayingOverlay.Visibility = Visibility.Visible;
            UpdateNowPlayingContent();
            if (NpVideoStage.Visibility == Visibility.Visible)
            {
                LoadCurrentVideo();
            }
        }
    }

    private void NowPlaying_Click(object sender, RoutedEventArgs e)
    {
        ToggleNowPlaying();
    }

    private void NpBackButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleNowPlaying();
    }

    private bool _isUserSeeking = false;

    private void UpdateNowPlayingContent()
    {
        try
        {
            var pb = ViewModel?.PlayerBar;
            var currentSong = ViewModel?.PlaybackService.CurrentSong;

            if (pb != null && NpTitleText != null)
            {
                NpTitleText.Text = !string.IsNullOrEmpty(pb.Title) ? pb.Title : "Not Playing";
                NpArtistText.Text = !string.IsNullOrEmpty(pb.Artist) ? pb.Artist : "Choose a song to start listening";

                if (!string.IsNullOrEmpty(pb.ThumbnailUrl) && Uri.TryCreate(pb.ThumbnailUrl, UriKind.Absolute, out var uri))
                {
                    NpAlbumBitmap.UriSource = uri;
                }

                if (NpPlayPauseIcon != null && ViewModel?.PlaybackService != null)
                {
                    NpPlayPauseIcon.Glyph = ViewModel.PlaybackService.IsPlaying ? "\uE769" : "\uE768";
                }

                if (ViewModel?.PlaybackService != null && NpPositionText != null && NpDurationText != null)
                {
                    var pos = ViewModel.PlaybackService.Position;
                    var dur = ViewModel.PlaybackService.Duration;
                    NpPositionText.Text = FormatTime(pos);
                    NpDurationText.Text = FormatTime(dur);
                    if (!_isUserSeeking && dur.TotalSeconds > 0)
                    {
                        NpPositionSlider.Value = (pos.TotalSeconds / dur.TotalSeconds) * 100.0;
                    }
                }
            }

            if (currentSong != null)
            {
                UpdateMeshGradients(currentSong.GradientColorStart, currentSong.GradientColorEnd);
            }

            if (SidebarNextTitle != null && SidebarNextArtist != null && ViewModel?.PlaybackService?.UpNext != null)
            {
                var next = System.Linq.Enumerable.FirstOrDefault(ViewModel.PlaybackService.UpNext);
                SidebarNextTitle.Text = next?.Title ?? "Auto Radio Mix";
                SidebarNextArtist.Text = next?.Artist ?? "Based on your listening taste";
            }
        }
        catch { }
    }

    private static string FormatTime(TimeSpan t)
    {
        return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
    }

    private void PlayerPlayPause_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.PlayerBar?.TogglePlayPauseCommand?.Execute(null);
        if (NpPlayPauseIcon != null && ViewModel?.PlaybackService != null)
        {
            NpPlayPauseIcon.Glyph = ViewModel.PlaybackService.IsPlaying ? "\uE768" : "\uE769";
        }
    }

    private async void PlayerNext_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.PlaybackService != null)
            await ViewModel.PlaybackService.PlayNextAsync();
    }

    private async void PlayerPrev_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.PlaybackService != null)
            await ViewModel.PlaybackService.PlayPreviousAsync();
    }

    private void PlayerShuffle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.PlaybackService != null)
            ViewModel.PlaybackService.IsShuffleEnabled = !ViewModel.PlaybackService.IsShuffleEnabled;
    }

    private void PlayerRepeat_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.PlaybackService != null)
            ViewModel.PlaybackService.RepeatMode = (ViewModel.PlaybackService.RepeatMode + 1) % 3;
    }

    private void NpPositionSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ViewModel?.PlaybackService != null && Math.Abs(e.NewValue - e.OldValue) > 1.0)
        {
            var dur = ViewModel.PlaybackService.Duration.TotalSeconds;
            if (dur > 0)
            {
                var targetSec = (e.NewValue / 100.0) * dur;
                ViewModel.PlaybackService.Seek(TimeSpan.FromSeconds(targetSec));
            }
        }
    }

    private DispatcherTimer? _ambientMeshTimer;
    private double _meshAngle = 0;

    private void StartAmbientMeshAnimation()
    {
        if (_ambientMeshTimer != null) return;
        _ambientMeshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _ambientMeshTimer.Tick += (s, e) =>
        {
            if (NowPlayingOverlay?.Visibility != Visibility.Visible) return;
            _meshAngle += 0.015;
            double xOff1 = Math.Cos(_meshAngle) * 60.0;
            double yOff1 = Math.Sin(_meshAngle) * 50.0;
            double xOff2 = -Math.Sin(_meshAngle * 0.8) * 70.0;
            double yOff2 = -Math.Cos(_meshAngle * 0.8) * 60.0;

            if (NpMeshEllipse1 != null)
                NpMeshEllipse1.Margin = new Thickness(-150 + xOff1, -150 + yOff1, 0, 0);
            if (NpMeshEllipse2 != null)
                NpMeshEllipse2.Margin = new Thickness(0, 0, -200 + xOff2, -200 + yOff2);
        };
        _ambientMeshTimer.Start();
    }

    private void UpdateMeshGradients(string hex1, string hex2)
    {
        try
        {
            var c1 = ParseHexColor(hex1);
            var c2 = ParseHexColor(hex2);
            NpMeshStop1.Color = Windows.UI.Color.FromArgb(140, c1.R, c1.G, c1.B);
            NpMeshStop2.Color = Windows.UI.Color.FromArgb(120, c2.R, c2.G, c2.B);
        }
        catch { }
    }

    private static Windows.UI.Color ParseHexColor(string hex)
    {
        try
        {
            if (string.IsNullOrEmpty(hex)) return Windows.UI.Color.FromArgb(255, 250, 45, 72);
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return Windows.UI.Color.FromArgb(255, r, g, b);
            }
        }
        catch { }
        return Windows.UI.Color.FromArgb(255, 250, 45, 72);
    }

    private void CloseNowPlaying_Click(object sender, RoutedEventArgs e)
    {
        NowPlayingOverlay.Visibility = Visibility.Collapsed;
        ContentFrame.Visibility = Visibility.Visible;
        FloatingPlayerContainer.Visibility = Visibility.Visible;
        PauseVideoPlayer();
    }

    private void NpStageSong_Click(object sender, RoutedEventArgs e)
    {
        NpSongStage.Visibility = Visibility.Visible;
        NpVideoStage.Visibility = Visibility.Collapsed;
        NpStageSongBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        NpStageVideoBtn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        PauseVideoPlayer();
        if (ViewModel?.PlaybackService != null && !ViewModel.PlaybackService.IsPlaying)
        {
            ViewModel.PlaybackService.Resume();
        }
    }

    private void NpStageVideo_Click(object sender, RoutedEventArgs e)
    {
        NpSongStage.Visibility = Visibility.Collapsed;
        NpVideoStage.Visibility = Visibility.Visible;
        NpStageVideoBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        NpStageSongBtn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        LoadCurrentVideo();
    }

    private string? _currentLoadedVideoId;
    private bool _isVideoInitialized = false;

    private async void LoadCurrentVideo()
    {
        var currentSong = ViewModel?.PlaybackService?.CurrentSong;
        if (currentSong == null || string.IsNullOrEmpty(currentSong.VideoId)) return;

        try
        {
            string videoId = currentSong.VideoId;
            bool isYouTubeId = videoId.Length == 11 && videoId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

            if (!isYouTubeId)
            {
                try
                {
                    var yt = new YoutubeExplode.YoutubeClient();
                    var query = $"{currentSong.Artist} {currentSong.Title}";
                    await foreach (var vid in yt.Search.GetVideosAsync(query))
                    {
                        videoId = vid.Id.Value;
                        break;
                    }
                }
                catch (System.Exception ex)
                {
                    Helpers.AppLogger.LogError("Failed to search YouTube video ID for video mode", ex);
                }
            }

            if (_currentLoadedVideoId == videoId && _isVideoInitialized)
            {
                try
                {
                    await NpVideoWebView.ExecuteScriptAsync("document.getElementById('ytplayer')?.contentWindow?.postMessage('{"event":"command","func":"playVideo","args":""}', '*');");
                }
                catch { }
                return;
            }

            _currentLoadedVideoId = videoId;

            // Pause background audio service so user only hears the music video soundtrack
            if (ViewModel?.PlaybackService != null && ViewModel.PlaybackService.IsPlaying)
            {
                ViewModel.PlaybackService.Pause();
            }

            await NpVideoWebView.EnsureCoreWebView2Async();
            _isVideoInitialized = true;

            string html = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        html, body {{ width: 100%; height: 100%; overflow: hidden; background-color: #000; }}
        iframe {{ width: 100%; height: 100%; border: none; }}
    </style>
</head>
<body>
    <iframe id='ytplayer' src='https://www.youtube.com/embed/{videoId}?autoplay=1&enablejsapi=1&origin=https://www.youtube.com' allow='autoplay; encrypted-media; picture-in-picture' allowfullscreen></iframe>
</body>
</html>";

            NpVideoWebView.NavigateToString(html);
            Helpers.AppLogger.Log("WebView2 video player loaded videoId: " + videoId);
        }
        catch (System.Exception ex)
        {
            Helpers.AppLogger.LogError("LoadCurrentVideo WebView2 failed", ex);
        }
    }

    private void PauseVideoPlayer()
    {
        try
        {
            if (_isVideoInitialized && NpVideoWebView?.CoreWebView2 != null)
            {
                _ = NpVideoWebView.ExecuteScriptAsync("document.getElementById('ytplayer')?.contentWindow?.postMessage('{"event":"command","func":"pauseVideo","args":""}', '*');");
            }
        }
        catch { }
    }

    private void SidebarTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            SwitchSidebarTab(tag);
        }
    }

    private void SwitchSidebarTab(string tab)
    {
        if (tab == "lyrics")
        {
            SidebarNowPlayingView.Visibility = Visibility.Collapsed;
            SidebarLyricsView.Visibility = Visibility.Visible;
            SidebarTabNowPlayingBtn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            SidebarTabNowPlayingBtn.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            SidebarTabLyricsBtn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            SidebarTabLyricsBtn.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
        else
        {
            SidebarLyricsView.Visibility = Visibility.Collapsed;
            SidebarNowPlayingView.Visibility = Visibility.Visible;
            SidebarTabLyricsBtn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            SidebarTabLyricsBtn.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            SidebarTabNowPlayingBtn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            SidebarTabNowPlayingBtn.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
    }

    private void SidebarPopout_Click(object sender, RoutedEventArgs e)
    {
        LyricsSplitView.IsPaneOpen = false;
        FloatingMiniPlayerOverlay.Visibility = Visibility.Visible;
    }

    private void SidebarClose_Click(object sender, RoutedEventArgs e)
    {
        LyricsSplitView.IsPaneOpen = false;
    }

    private void FloatingDockToSidebar_Click(object sender, RoutedEventArgs e)
    {
        FloatingMiniPlayerOverlay.Visibility = Visibility.Collapsed;
        SwitchSidebarTab("nowplaying");
        LyricsSplitView.IsPaneOpen = true;
    }

    private void FloatingClose_Click(object sender, RoutedEventArgs e)
    {
        FloatingMiniPlayerOverlay.Visibility = Visibility.Collapsed;
    }

    private void FloatingMiniPlayer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        FloatingMiniPlayerOverlay.Opacity = 1.0;
        FloatingTransportRow.Visibility = Visibility.Visible;
    }

    private void FloatingMiniPlayer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
    }

    private void QueueOpen_Click(object sender, RoutedEventArgs e)
    {
        PlayerBarControl.OpenQueueFlyout();
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
                appWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveForegroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Transparent;
            }
        }
        catch { }
    }

    private void ApplyLocalization()
    {
        try
        {
            var loc = LocalizationService.Instance;
            NavHomeText.Text = loc.GetString("Home");
            NavExploreText.Text = loc.GetString("Explore");
            NavLibraryText.Text = loc.GetString("Library");
            NavDownloadsText.Text = loc.GetString("Downloads");
            NavSettingsText.Text = loc.GetString("Settings");
            UserAccountStatusTitle.Text = loc.GetString("GoogleSync");
        }
        catch { }
    }

    public void SetBackdrop(string backdropType)
    {
        try
        {
            if (backdropType == "Acrylic" && Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            else if (backdropType == "Mica" && Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop
                {
                    Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
                };
            }
            else if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop
                {
                    Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
                };
            }
        }
        catch { }
    }

    private void TrySetBackdrop()
    {
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        string material = localSettings.Values["WindowMaterial"] as string ?? "MicaAlt";
        SetBackdrop(material);
    }

    private void SidebarToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        if (_isSidebarCollapsed)
        {
            SidebarColDef.Width = new GridLength(0);
            SidebarPane.Visibility = Visibility.Collapsed;
            CollapsedSidebarRevealBtn.Visibility = Visibility.Visible;
            SidebarPeekTrigger.Visibility = Visibility.Visible;
        }
        else
        {
            SidebarColDef.Width = new GridLength(220);
            SidebarPane.Visibility = Visibility.Visible;
            CollapsedSidebarRevealBtn.Visibility = Visibility.Collapsed;
            SidebarPeekTrigger.Visibility = Visibility.Collapsed;
        }
    }

    private void SidebarPeekTrigger_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_isSidebarCollapsed)
        {
            // Hover peek sidebar
            SidebarColDef.Width = new GridLength(220);
            SidebarPane.Visibility = Visibility.Visible;
        }
    }

    private void CaptionControls_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        CaptionControlsBorder.Opacity = 1.0;
    }

    private void CaptionControls_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        CaptionControlsBorder.Opacity = 0.0;
    }

    private void WinCloseBtn_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void WinMinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.Minimize();
            }
        }
        catch { }
    }

    private void WinMaximizeBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                {
                    presenter.Restore();
                    WinMaximizeGlyph.Glyph = "\uE922";
                }
                else
                {
                    presenter.Maximize();
                    WinMaximizeGlyph.Glyph = "\uE923";
                }
            }
        }
        catch { }
    }

    private void SetActiveNavButton(int index)
    {
        var activeBg = new SolidColorBrush(Windows.UI.Color.FromArgb(0x28, 0xFA, 0x2D, 0x48));
        var activeBorder = new SolidColorBrush(Windows.UI.Color.FromArgb(0x55, 0xFA, 0x2D, 0x48));
        var transBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        var buttons = new[] { NavHomeBtn, NavExploreBtn, NavLibraryBtn, NavSettingsBtn, NavDownloadsBtn };
        for (int i = 0; i < buttons.Length; i++)
        {
            bool isActive = (i == index);
            buttons[i].Background = isActive ? activeBg : transBrush;
            buttons[i].BorderBrush = isActive ? activeBorder : transBrush;
            buttons[i].BorderThickness = new Thickness(1);
            buttons[i].CornerRadius = new CornerRadius(12);
        }
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

    private void NavLibraryChevron_Click(object sender, RoutedEventArgs e)
    {
        bool isVisible = NavDownloadsBtn.Visibility == Visibility.Visible;
        NavDownloadsBtn.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
        NavLibraryChevron.Glyph = isVisible ? "\uE70D" : "\uE70E";
    }

    private void NavDownloads_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(DownloadsPage));
        SetActiveNavButton(4);
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
