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
        ContentFrame.Navigated += (s, e) => CheckGoogleSignInStatus();

        // Sync native video player state with media playback service
        ViewModel.PlaybackService.PlaybackStateChanged += (s, state) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (NpVideoStage.Visibility == Visibility.Visible && _nativeVideoPlayer != null)
                {
                    if (state == MediaPlaybackState.Playing && _nativeVideoPlayer.PlaybackSession.PlaybackState != Windows.Media.Playback.MediaPlaybackState.Playing)
                    {
                        _nativeVideoPlayer.Play();
                    }
                    else if (state == MediaPlaybackState.Paused && _nativeVideoPlayer.PlaybackSession.PlaybackState != Windows.Media.Playback.MediaPlaybackState.Paused)
                    {
                        _nativeVideoPlayer.Pause();
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

    private Windows.Media.Playback.MediaPlayer? _nativeVideoPlayer;
    private DispatcherTimer? _videoSyncTimer;

    private void VideoAudioSyncToggle_Click(object sender, RoutedEventArgs e)
    {
        bool isSync = VideoAudioSyncToggle.IsChecked == true;
        if (isSync)
        {
            SyncToggleText.Text = "FLAC Synced";
            SyncToggleIcon.Glyph = "\uE8D6";
            if (_nativeVideoPlayer != null)
            {
                _nativeVideoPlayer.IsMuted = true;
            }
            ViewModel.PlaybackService.Resume();
        }
        else
        {
            SyncToggleText.Text = "Video Audio";
            SyncToggleIcon.Glyph = "\uE714";
            if (_nativeVideoPlayer != null)
            {
                _nativeVideoPlayer.IsMuted = false;
            }
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

            var ytClient = new YoutubeExplode.YoutubeClient();
            var manifest = await ytClient.Videos.Streams.GetManifestAsync(videoId);

            // Prefer high-quality MP4 muxed stream (video + audio)
            var muxed = manifest.GetMuxedStreams()
                .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                .OrderByDescending(s => s.VideoQuality)
                .FirstOrDefault();

            string? videoUrl = muxed?.Url;
            if (string.IsNullOrEmpty(videoUrl))
            {
                var vidOnly = manifest.GetVideoOnlyStreams()
                    .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                    .OrderByDescending(s => s.VideoQuality)
                    .FirstOrDefault();
                videoUrl = vidOnly?.Url ?? manifest.GetMuxedStreams().FirstOrDefault()?.Url;
            }

            if (string.IsNullOrEmpty(videoUrl))
            {
                Helpers.AppLogger.Log("No native video stream available for " + videoId);
                return;
            }

            if (_nativeVideoPlayer == null)
            {
                _nativeVideoPlayer = new Windows.Media.Playback.MediaPlayer();
                _nativeVideoPlayer.AudioCategory = Windows.Media.Playback.MediaPlayerAudioCategory.Media;
                _nativeVideoPlayer.PlaybackSession.PlaybackStateChanged += (sender, args) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (VideoAudioSyncToggle.IsChecked == true)
                        {
                            if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Buffering)
                            {
                                ViewModel.PlaybackService.Pause();
                            }
                            else if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing && !ViewModel.PlaybackService.IsPlaying)
                            {
                                ViewModel.PlaybackService.Resume();
                            }
                        }
                    });
                };
                NpVideoPlayer.SetMediaPlayer(_nativeVideoPlayer);

                _videoSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _videoSyncTimer.Tick += (s, e) =>
                {
                    if (NpVideoStage.Visibility == Visibility.Visible &&
                        VideoAudioSyncToggle.IsChecked == true &&
                        _nativeVideoPlayer != null &&
                        ViewModel.PlaybackService.IsPlaying)
                    {
                        var audioPos = ViewModel.PlaybackService.Position;
                        var videoPos = _nativeVideoPlayer.PlaybackSession.Position;
                        var drift = Math.Abs((videoPos - audioPos).TotalMilliseconds);
                        if (drift > 350)
                        {
                            _nativeVideoPlayer.PlaybackSession.Position = audioPos;
                        }
                    }
                };
                _videoSyncTimer.Start();
            }

            bool muteVideo = VideoAudioSyncToggle.IsChecked == true;
            _nativeVideoPlayer.IsMuted = muteVideo;

            var mediaProxy = App.Services.GetRequiredService<LocalMediaProxy>();
            string proxiedUrl = mediaProxy.GetProxiedStreamUrl(videoUrl);
            _nativeVideoPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(proxiedUrl));

            if (ViewModel.PlaybackService.Position > TimeSpan.Zero)
            {
                _nativeVideoPlayer.PlaybackSession.Position = ViewModel.PlaybackService.Position;
            }

            _nativeVideoPlayer.Play();
            Helpers.AppLogger.Log("Native video player started for videoId: " + videoId);
        }
        catch (System.Exception ex)
        {
            Helpers.AppLogger.LogError("LoadCurrentVideo native player failed", ex);
        }
    }

    private void PauseVideoPlayer()
    {
        try
        {
            _nativeVideoPlayer?.Pause();
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

    private void MacCloseBtn_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void MacMinimizeBtn_Click(object sender, RoutedEventArgs e)
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

    private void MacMaximizeBtn_Click(object sender, RoutedEventArgs e)
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
                }
                else
                {
                    presenter.Maximize();
                }
            }
        }
        catch { }
    }
}
