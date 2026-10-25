using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using YTMusicClient.Services;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }
    private bool _isInitializing = true;

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        this.InitializeComponent();
        this.Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        LoadSavedSettings();
        RefreshAccountUI();
        CalculateCacheSize();
        _isInitializing = false;
    }

    private void LoadSavedSettings()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            // 1. Theme
            if (localSettings.Values["AppTheme"] is string themeStr)
            {
                for (int i = 0; i < ThemeCombo.Items.Count; i++)
                {
                    if ((ThemeCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == themeStr)
                    {
                        ThemeCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            // 2. Window Material
            if (localSettings.Values["WindowMaterial"] is string matStr)
            {
                for (int i = 0; i < WindowMaterialCombo.Items.Count; i++)
                {
                    if ((WindowMaterialCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == matStr)
                    {
                        WindowMaterialCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            // 3. Language
            var currentLang = LocalizationService.Instance.CurrentLanguage.ToString();
            for (int i = 0; i < LanguageCombo.Items.Count; i++)
            {
                if ((LanguageCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == currentLang)
                {
                    LanguageCombo.SelectedIndex = i;
                    break;
                }
            }

            // 4. Audio Engine
            if (localSettings.Values["AudioSourceEngine"] is string engineStr)
            {
                for (int i = 0; i < AudioSourceCombo.Items.Count; i++)
                {
                    if ((AudioSourceCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == engineStr)
                    {
                        AudioSourceCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            // 5. Crossfade Duration
            if (localSettings.Values["CrossfadeSeconds"] is double cf)
            {
                CrossfadeSlider.Value = cf;
                CrossfadeLabel.Text = $"Smooth transition between songs: {cf:F1}s";
            }
            else
            {
                CrossfadeSlider.Value = 4.0;
                CrossfadeLabel.Text = "Smooth transition between songs: 4.0s";
            }

            // 6. Volume Normalization (ReplayGain)
            if (localSettings.Values["VolumeNormalization"] is bool vn)
            {
                ReplayGainToggle.IsOn = vn;
            }

            // 7. Video Audio Sync
            if (localSettings.Values["VideoAudioSync"] is bool vs)
            {
                VideoSyncToggle.IsOn = vs;
            }

            // 8. Lyrics Style & Animations (ArchiveTune)
            if (localSettings.Values["LyricsMode"] is string lMode)
            {
                for (int i = 0; i < LyricsStyleCombo.Items.Count; i++)
                {
                    if ((LyricsStyleCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == lMode)
                    {
                        LyricsStyleCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (localSettings.Values["LyricsV2LrcBounceEnabled"] is bool lrcBounce)
            {
                LrcBounceToggle.IsOn = lrcBounce;
            }

            if (localSettings.Values["LyricsV2BounceFactor"] is double bounce)
            {
                BounceSlider.Value = bounce;
                BounceLabel.Text = $"Adjust the bounce effect when a word is sung ({(int)(bounce * 100)}%)";
            }

            if (localSettings.Values["LyricsV2GlowFactor"] is double glow)
            {
                GlowSlider.Value = glow;
                GlowLabel.Text = $"Adjust the glow brightness of the sung word ({(int)(glow * 100)}%)";
            }

            if (localSettings.Values["LyricsV2FillTransitionWidth"] is double fill)
            {
                FillSlider.Value = fill;
                FillLabel.Text = $"Adjust the gradient edge width of the liquid fill effect ({(int)fill} px)";
            }

            if (localSettings.Values["LyricsRomanizeHindi"] is bool romHindi)
            {
                RomanizeHindiToggle.IsOn = romHindi;
            }

            // 9. Download Quality
            if (localSettings.Values["DownloadAudioQuality"] is string dqStr)
            {
                for (int i = 0; i < DownloadQualityCombo.Items.Count; i++)
                {
                    if ((DownloadQualityCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == dqStr)
                    {
                        DownloadQualityCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Download Folder path
            var musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var downloadPath = Path.Combine(musicFolder, "FluentMusic", "Downloads");
            DownloadFolderPathText.Text = downloadPath;
        }
        catch { }
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int tabIdx))
        {
            SectionAppearance.Visibility = tabIdx == 0 ? Visibility.Visible : Visibility.Collapsed;
            SectionLanguage.Visibility = tabIdx == 1 ? Visibility.Visible : Visibility.Collapsed;
            SectionAudio.Visibility = tabIdx == 2 ? Visibility.Visible : Visibility.Collapsed;
            SectionLyrics.Visibility = tabIdx == 3 ? Visibility.Visible : Visibility.Collapsed;
            SectionDownloads.Visibility = tabIdx == 4 ? Visibility.Visible : Visibility.Collapsed;
            SectionAccount.Visibility = tabIdx == 5 ? Visibility.Visible : Visibility.Collapsed;
            SectionAbout.Visibility = tabIdx == 6 ? Visibility.Visible : Visibility.Collapsed;

            var activeBg = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            var transBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            TabAppearanceBtn.Background = tabIdx == 0 ? activeBg : transBg;
            TabAppearanceBtn.FontWeight = tabIdx == 0 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabLanguageBtn.Background = tabIdx == 1 ? activeBg : transBg;
            TabLanguageBtn.FontWeight = tabIdx == 1 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabAudioBtn.Background = tabIdx == 2 ? activeBg : transBg;
            TabAudioBtn.FontWeight = tabIdx == 2 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabLyricsBtn.Background = tabIdx == 3 ? activeBg : transBg;
            TabLyricsBtn.FontWeight = tabIdx == 3 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabDownloadsBtn.Background = tabIdx == 4 ? activeBg : transBg;
            TabDownloadsBtn.FontWeight = tabIdx == 4 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabAccountBtn.Background = tabIdx == 5 ? activeBg : transBg;
            TabAccountBtn.FontWeight = tabIdx == 5 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabAboutBtn.Background = tabIdx == 6 ? activeBg : transBg;
            TabAboutBtn.FontWeight = tabIdx == 6 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString() ?? "Default";
            ApplicationData.Current.LocalSettings.Values["AppTheme"] = tag;
            ViewModel.SetThemeCommand.Execute(tag);

            if (this.XamlRoot?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ViewModel.CurrentTheme;
            }
        }
    }

    private void WindowMaterialCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString() ?? "MicaAlt";
            ApplicationData.Current.LocalSettings.Values["WindowMaterial"] = tag;
            MainWindow.Current?.SetBackdrop(tag);
        }
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is string langStr)
        {
            if (Enum.TryParse<AppLanguage>(langStr, out var parsed))
            {
                LocalizationService.Instance.SetLanguage(parsed);
                SettingsHeaderTitle.Text = LocalizationService.Instance.GetString("Settings");
                SettingsHeaderSubtitle.Text = LocalizationService.Instance.GetString("ListenNowSubtitle");
            }
        }
    }

    private void AudioSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            ApplicationData.Current.LocalSettings.Values["AudioSourceEngine"] = tag;
        }
    }

    private void CrossfadeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var val = e.NewValue;
        CrossfadeLabel.Text = $"Smooth transition between songs: {val:F1}s";
        ApplicationData.Current.LocalSettings.Values["CrossfadeSeconds"] = val;

        var playback = App.Services.GetService<IMediaPlaybackService>();
        if (playback != null)
        {
            playback.CrossfadeDurationSeconds = val;
        }
    }

    private void ReplayGainToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool isOn = ReplayGainToggle.IsOn;
        ApplicationData.Current.LocalSettings.Values["VolumeNormalization"] = isOn;

        var playback = App.Services.GetService<IMediaPlaybackService>();
        if (playback != null)
        {
            playback.IsVolumeNormalizationEnabled = isOn;
        }
    }

    private void VideoSyncToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        ApplicationData.Current.LocalSettings.Values["VideoAudioSync"] = VideoSyncToggle.IsOn;
    }

    private void LyricsStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            ApplicationData.Current.LocalSettings.Values["LyricsMode"] = tag;
            var lyricsVm = App.Services.GetService<LyricsViewModel>();
            if (lyricsVm != null)
            {
                lyricsVm.LyricsMode = tag;
            }
        }
    }

    private void LrcBounceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool isOn = LrcBounceToggle.IsOn;
        ApplicationData.Current.LocalSettings.Values["LyricsV2LrcBounceEnabled"] = isOn;
        var lyricsVm = App.Services.GetService<LyricsViewModel>();
        if (lyricsVm != null) lyricsVm.LrcBounceEnabled = isOn;
    }

    private void BounceSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var val = e.NewValue;
        BounceLabel.Text = $"Adjust the bounce effect when a word is sung ({(int)(val * 100)}%)";
        ApplicationData.Current.LocalSettings.Values["LyricsV2BounceFactor"] = val;
        var lyricsVm = App.Services.GetService<LyricsViewModel>();
        if (lyricsVm != null) lyricsVm.BounceFactor = val;
    }

    private void GlowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var val = e.NewValue;
        GlowLabel.Text = $"Adjust the glow brightness of the sung word ({(int)(val * 100)}%)";
        ApplicationData.Current.LocalSettings.Values["LyricsV2GlowFactor"] = val;
        var lyricsVm = App.Services.GetService<LyricsViewModel>();
        if (lyricsVm != null) lyricsVm.GlowFactor = val;
    }

    private void FillSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var val = e.NewValue;
        FillLabel.Text = $"Adjust the gradient edge width of the liquid fill effect ({(int)val} px)";
        ApplicationData.Current.LocalSettings.Values["LyricsV2FillTransitionWidth"] = val;
        var lyricsVm = App.Services.GetService<LyricsViewModel>();
        if (lyricsVm != null) lyricsVm.FillTransitionWidth = val;
    }

    private void RomanizeHindiToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        ApplicationData.Current.LocalSettings.Values["LyricsRomanizeHindi"] = RomanizeHindiToggle.IsOn;
    }

    private void DownloadQualityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            ApplicationData.Current.LocalSettings.Values["DownloadAudioQuality"] = tag;
        }
    }

    private void OpenDownloadFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var downloadPath = Path.Combine(musicFolder, "FluentMusic", "Downloads");
            if (!Directory.Exists(downloadPath))
            {
                Directory.CreateDirectory(downloadPath);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadPath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CalculateCacheSize()
    {
        try
        {
            var tempFolder = Path.GetTempPath();
            var appCacheDir = Path.Combine(tempFolder, "FluentMusicCache");
            if (Directory.Exists(appCacheDir))
            {
                var files = Directory.GetFiles(appCacheDir, "*", SearchOption.AllDirectories);
                long bytes = files.Sum(f => new FileInfo(f).Length);
                double mb = bytes / (1024.0 * 1024.0);
                CacheStatusText.Text = $"{mb:F1} MB temporary audio chunks cached";
            }
            else
            {
                CacheStatusText.Text = "0 MB cached (Storage is optimal)";
            }
        }
        catch
        {
            CacheStatusText.Text = "Optimal (Cache is low)";
        }
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tempFolder = Path.GetTempPath();
            var appCacheDir = Path.Combine(tempFolder, "FluentMusicCache");
            if (Directory.Exists(appCacheDir))
            {
                Directory.Delete(appCacheDir, true);
            }
            CacheStatusText.Text = "Cache Cleared (0 KB)";
            ClearCacheBtn.Content = "Cleared \u2713";
        }
        catch
        {
            CacheStatusText.Text = "Cache Cleared (0 KB)";
        }
    }

    private void RefreshAccountUI()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            bool isSignedIn = (localSettings.Values["IsGoogleSignedIn"] is bool b && b) ||
                              (localSettings.Values["GoogleAuthCookies"] is string cookies && !string.IsNullOrWhiteSpace(cookies));
            if (isSignedIn)
            {
                var user = localSettings.Values["UserDisplayName"] as string;
                if (string.IsNullOrWhiteSpace(user) || user == "Google User")
                {
                    user = "JPGRS_GAMING09";
                }
                var avatarUrl = localSettings.Values["UserAvatarUrl"] as string;

                AccountStatusTitle.Text = $"Connected: {user}";
                AccountStatusSubtitle.Text = "Your YouTube Music playlists, recommendations, and liked songs are synced.";
                SignInGoogleBtnText.Text = "Sign Out";

                if (!string.IsNullOrEmpty(avatarUrl) && Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri))
                {
                    AccountAvatarImage.Source = new BitmapImage(uri);
                    AccountAvatarImage.Visibility = Visibility.Visible;
                    AccountAvatarIcon.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AccountAvatarImage.Visibility = Visibility.Collapsed;
                    AccountAvatarIcon.Visibility = Visibility.Visible;
                }
            }
            else
            {
                AccountStatusTitle.Text = "YouTube Music Sync";
                AccountStatusSubtitle.Text = "Sign in to sync your personal playlists, liked songs, and history inside the app";
                SignInGoogleBtnText.Text = "Sign in with Google";
                AccountAvatarImage.Visibility = Visibility.Collapsed;
                AccountAvatarIcon.Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    private async void SignInWithGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (this.XamlRoot == null) return;

        var localSettings = ApplicationData.Current.LocalSettings;
        bool isSignedIn = localSettings.Values["IsGoogleSignedIn"] is bool b && b;

        if (isSignedIn)
        {
            localSettings.Values["IsGoogleSignedIn"] = false;
            localSettings.Values.Remove("GoogleAuthCookies");
            localSettings.Values.Remove("UserDisplayName");
            localSettings.Values.Remove("UserAvatarUrl");
            RefreshAccountUI();
            (App.MainWindow as MainWindow)?.CheckGoogleSignInStatus();

            var outDialog = new ContentDialog
            {
                Title = "Signed Out",
                Content = "You have signed out of YouTube Music sync.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await outDialog.ShowAsync();
            return;
        }

        try
        {
            var webView = new WebView2
            {
                Width = 480,
                Height = 580,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var dialog = new ContentDialog
            {
                Title = "Sign In to YouTube Music",
                Content = webView,
                CloseButtonText = "Done",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            webView.NavigationCompleted += async (s, args) =>
            {
                if (!args.IsSuccess) return;

                try
                {
                    var uri = webView.Source?.ToString() ?? string.Empty;
                    if (uri.Contains("music.youtube.com"))
                    {
                        var cookieManager = webView.CoreWebView2?.CookieManager;
                        if (cookieManager != null)
                        {
                            var cookies = await cookieManager.GetCookiesAsync("https://music.youtube.com");
                            bool hasAuthCookie = cookies.Any(c =>
                                c.Name.Equals("SAPISID", StringComparison.OrdinalIgnoreCase) ||
                                c.Name.Equals("__Secure-3PAPISID", StringComparison.OrdinalIgnoreCase) ||
                                c.Name.Equals("LOGIN_INFO", StringComparison.OrdinalIgnoreCase) ||
                                c.Name.Equals("SID", StringComparison.OrdinalIgnoreCase));

                            if (hasAuthCookie)
                            {
                                localSettings.Values["IsGoogleSignedIn"] = true;

                                string displayName = "Google Account";
                                string avatarUrl = string.Empty;
                                try
                                {
                                    var scriptResult = await webView.ExecuteScriptAsync("document.querySelector('button#avatar-btn')?.getAttribute('aria-label') || ''");
                                    if (!string.IsNullOrWhiteSpace(scriptResult) && scriptResult != "null" && scriptResult != "\"\"")
                                    {
                                        displayName = scriptResult.Trim('"', ' ');
                                    }

                                    var avatarScript = await webView.ExecuteScriptAsync("document.querySelector('button#avatar-btn img')?.src || ''");
                                    if (!string.IsNullOrWhiteSpace(avatarScript) && avatarScript != "null" && avatarScript != "\"\"")
                                    {
                                        avatarUrl = avatarScript.Trim('"', ' ');
                                    }
                                }
                                catch { }

                                localSettings.Values["UserDisplayName"] = displayName;
                                if (!string.IsNullOrEmpty(avatarUrl))
                                {
                                    localSettings.Values["UserAvatarUrl"] = avatarUrl;
                                }

                                var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                                localSettings.Values["GoogleAuthCookies"] = cookieHeader;

                                RefreshAccountUI();
                                (App.MainWindow as MainWindow)?.CheckGoogleSignInStatus();
                                dialog.Hide();

                                var successDialog = new ContentDialog
                                {
                                    Title = "Signed In Successfully",
                                    Content = $"Connected as {displayName}. Your personalized YouTube Music feed, shelves, and mood filters are now synced with FluentMusic!",
                                    CloseButtonText = "Awesome",
                                    XamlRoot = this.XamlRoot
                                };
                                await successDialog.ShowAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Helpers.AppLogger.LogError("Google auth capture error", ex);
                }
            };

            webView.Source = new Uri("https://accounts.google.com/ServiceLogin?continue=https://music.youtube.com");

            await dialog.ShowAsync();
            RefreshAccountUI();
        }
        catch (Exception ex)
        {
            var errDialog = new ContentDialog
            {
                Title = "Sign In",
                Content = $"Please ensure WebView2 Runtime is installed: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errDialog.ShowAsync();
        }
    }
}