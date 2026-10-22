using System;
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

        ViewModel.PlayerBar.PropertyChanged += (s, e) => UpdateNowPlayingContent();

        this.Activated += (s, e) => CheckGoogleSignInStatus();

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
            bool isSignedIn = localSettings.Values["IsGoogleSignedIn"] is bool b && b;
            if (isSignedIn)
            {
                var userName = localSettings.Values["UserDisplayName"] as string ?? "Google User";
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
            PauseVideoPlayer();
        }
        else
        {
            ContentFrame.Visibility = Visibility.Collapsed;
            NowPlayingOverlay.Visibility = Visibility.Visible;
            UpdateNowPlayingContent();
            if (NpVideoStage.Visibility == Visibility.Visible)
            {
                LoadCurrentVideo();
            }
        }
    }

    private bool _isVideoWebViewConfigured;

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
                NpQualityText.Text = !string.IsNullOrEmpty(pb.AudioQualityBadge) ? pb.AudioQualityBadge : "256 kbps · AAC · Hi-Res";
                NpFidelityText.Text = pb.FidelityBadgeText;
                NpFidelityPill.Background = pb.FidelityBadgeBackground;
                NpFidelityText.Foreground = pb.FidelityBadgeForeground;

                if (!string.IsNullOrEmpty(pb.ThumbnailUrl) && Uri.TryCreate(pb.ThumbnailUrl, UriKind.Absolute, out var uri))
                {
                    NpAlbumBitmap.UriSource = uri;
                }
            }

            if (currentSong != null)
            {
                UpdateMeshGradients(currentSong.GradientColorStart, currentSong.GradientColorEnd);
            }
        }
        catch { }
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

    private async void VideoAudioSyncToggle_Click(object sender, RoutedEventArgs e)
    {
        bool isSync = VideoAudioSyncToggle.IsChecked == true;
        if (isSync)
        {
            SyncToggleText.Text = "FLAC Synced";
            SyncToggleIcon.Glyph = "\uE8D6";
            try
            {
                await NpVideoWebView.ExecuteScriptAsync("if(player && typeof player.mute === 'function') player.mute();");
            }
            catch { }
            ViewModel.PlaybackService.Resume();
        }
        else
        {
            SyncToggleText.Text = "Video Audio";
            SyncToggleIcon.Glyph = "\uE714";
            try
            {
                await NpVideoWebView.ExecuteScriptAsync("if(player && typeof player.unMute === 'function') player.unMute();");
            }
            catch { }
            ViewModel.PlaybackService.Pause();
        }
    }

    private async void LoadCurrentVideo()
    {
        var currentSong = ViewModel.PlaybackService.CurrentSong;
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
                    Helpers.AppLogger.LogError("Failed to resolve YouTube video ID for video mode", ex);
                }
            }

            await NpVideoWebView.EnsureCoreWebView2Async();
            if (!_isVideoWebViewConfigured)
            {
                NpVideoWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                NpVideoWebView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

                NpVideoWebView.CoreWebView2.AddWebResourceRequestedFilter("https://www.youtube.com/*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
                NpVideoWebView.CoreWebView2.AddWebResourceRequestedFilter("https://www.youtube-nocookie.com/*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
                NpVideoWebView.CoreWebView2.WebResourceRequested += (s, args) =>
                {
                    args.Request.Headers.SetHeader("Referer", "https://www.youtube.com/");
                    args.Request.Headers.SetHeader("Origin", "https://www.youtube.com");
                };

                NpVideoWebView.WebMessageReceived += OnVideoWebMessageReceived;
                _isVideoWebViewConfigured = true;
            }

            bool muteVideo = VideoAudioSyncToggle.IsChecked == true;
            string muteParam = muteVideo ? "true" : "false";

            // YouTube Iframe API player template with strict origin and no-cookie host (Fixes Error 153)
            string playerHtml = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='referrer' content='strict-origin-when-cross-origin'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; overflow: hidden; background: #000; }}
        html, body, #player {{ width: 100%; height: 100%; }}
    </style>
</head>
<body>
    <div id='player'></div>
    <script src='https://www.youtube.com/iframe_api'></script>
    <script>
        var player;
        var isMuted = {muteParam};
        function onYouTubeIframeAPIReady() {{
            player = new YT.Player('player', {{
                height: '100%',
                width: '100%',
                videoId: '{videoId}',
                host: 'https://www.youtube-nocookie.com',
                playerVars: {{
                    'autoplay': 1,
                    'playsinline': 1,
                    'controls': 1,
                    'origin': 'https://www.youtube.com',
                    'widget_referrer': 'https://www.youtube.com',
                    'rel': 0,
                    'enablejsapi': 1
                }},
                events: {{
                    'onReady': onPlayerReady,
                    'onStateChange': onPlayerStateChange
                }}
            }});
        }}
        function onPlayerReady(event) {{
            if (isMuted) {{
                player.mute();
            }} else {{
                player.unMute();
            }}
            window.chrome.webview.postMessage(JSON.stringify({{ type: 'ready' }}));
        }}
        function onPlayerStateChange(event) {{
            window.chrome.webview.postMessage(JSON.stringify({{
                type: 'stateChange',
                state: event.data,
                currentTime: player ? player.getCurrentTime() : 0
            }}));
        }}
        setInterval(function() {{
            if (player && typeof player.getPlayerState === 'function' && player.getPlayerState() === 1) {{
                window.chrome.webview.postMessage(JSON.stringify({{
                    type: 'timeUpdate',
                    currentTime: player.getCurrentTime()
                }}));
            }}
        }}, 400);
    </script>
</body>
</html>";

            NpVideoWebView.NavigateToString(playerHtml);
        }
        catch (System.Exception ex)
        {
            Helpers.AppLogger.LogError("LoadCurrentVideo failed", ex);
        }
    }

    private void OnVideoWebMessageReceived(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var json = args.TryGetWebMessageAsString();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? "";

            bool isSyncMode = VideoAudioSyncToggle.IsChecked == true;

            if (type == "stateChange")
            {
                int state = root.GetProperty("state").GetInt32();
                double currentTime = root.TryGetProperty("currentTime", out var ct) ? ct.GetDouble() : 0;

                if (isSyncMode)
                {
                    // YT.PlayerState: 1 = Playing, 2 = Paused, 3 = Buffering, 0 = Ended
                    if (state == 1) // Video Playing -> Play high quality FLAC audio in sync
                    {
                        var audioPos = ViewModel.PlaybackService.Position.TotalSeconds;
                        if (Math.Abs(audioPos - currentTime) > 0.4)
                        {
                            ViewModel.PlaybackService.Seek(TimeSpan.FromSeconds(currentTime));
                        }
                        if (!ViewModel.PlaybackService.IsPlaying)
                        {
                            ViewModel.PlaybackService.Resume();
                        }
                    }
                    else if (state == 2 || state == 3) // Video Paused or Buffering -> Pause audio
                    {
                        if (ViewModel.PlaybackService.IsPlaying)
                        {
                            ViewModel.PlaybackService.Pause();
                        }
                    }
                }
            }
            else if (type == "timeUpdate" && isSyncMode)
            {
                double currentTime = root.GetProperty("currentTime").GetDouble();
                var audioPos = ViewModel.PlaybackService.Position.TotalSeconds;
                if (Math.Abs(audioPos - currentTime) > 0.45)
                {
                    ViewModel.PlaybackService.Seek(TimeSpan.FromSeconds(currentTime));
                }
            }
        }
        catch { }
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
        NavDownloadsBtn.Background = index == 4 ? activeBg : inactiveBg;
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
        LibraryChevronIcon.Glyph = isVisible ? "\uE70D" : "\uE70E";
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
