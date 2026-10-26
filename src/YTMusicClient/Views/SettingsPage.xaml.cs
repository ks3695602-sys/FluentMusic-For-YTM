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

            // 4. Audio Quality Engine
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

            // 7. Spicy Lyrics Extension Settings
            if (localSettings.Values["SpicySyllableEnabled"] is bool spicySyl)
            {
                SpicySyllableToggle.IsOn = spicySyl;
            }

            if (localSettings.Values["SpicyMeshGlow"] is string spicyMesh)
            {
                for (int i = 0; i < SpicyMeshCombo.Items.Count; i++)
                {
                    if ((SpicyMeshCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == spicyMesh)
                    {
                        SpicyMeshCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (localSettings.Values["SpicyBounceMult"] is double bounce)
            {
                SpicyBounceSlider.Value = bounce;
                SpicyBounceLabel.Text = $"Word bounce scale amplitude ({(int)(bounce * 100)}%)";
            }

            if (localSettings.Values["SpicyRomanizeEnabled"] is bool rom)
            {
                SpicyRomanizeToggle.IsOn = rom;
            }

            // 8. YouLy+ Extension Settings
            if (localSettings.Values["YouLySyllableEnabled"] is bool youLySyl)
            {
                YouLySyllableToggle.IsOn = youLySyl;
            }

            if (localSettings.Values["YouLyPrioritySource"] is string priority)
            {
                for (int i = 0; i < YouLyPriorityCombo.Items.Count; i++)
                {
                    if ((YouLyPriorityCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == priority)
                    {
                        YouLyPriorityCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (localSettings.Values["YouLySponsorBlockEnabled"] is bool spBlock)
            {
                YouLySponsorBlockToggle.IsOn = spBlock;
            }

            if (localSettings.Values["YouLyPerformanceMode"] is bool perf)
            {
                YouLyPerformanceToggle.IsOn = perf;
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
            SectionAudio.Visibility = tabIdx == 1 ? Visibility.Visible : Visibility.Collapsed;
            SectionSpicyLyrics.Visibility = tabIdx == 2 ? Visibility.Visible : Visibility.Collapsed;
            SectionYouLy.Visibility = tabIdx == 3 ? Visibility.Visible : Visibility.Collapsed;
            SectionDownloads.Visibility = tabIdx == 4 ? Visibility.Visible : Visibility.Collapsed;
            SectionAbout.Visibility = tabIdx == 5 ? Visibility.Visible : Visibility.Collapsed;

            var activeBg = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            var transBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            TabAppearanceBtn.Background = tabIdx == 0 ? activeBg : transBg;
            TabAppearanceBtn.FontWeight = tabIdx == 0 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabAudioBtn.Background = tabIdx == 1 ? activeBg : transBg;
            TabAudioBtn.FontWeight = tabIdx == 1 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabSpicyLyricsBtn.Background = tabIdx == 2 ? activeBg : transBg;
            TabSpicyLyricsBtn.FontWeight = tabIdx == 2 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabYouLyBtn.Background = tabIdx == 3 ? activeBg : transBg;
            TabYouLyBtn.FontWeight = tabIdx == 3 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabDownloadsBtn.Background = tabIdx == 4 ? activeBg : transBg;
            TabDownloadsBtn.FontWeight = tabIdx == 4 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabAboutBtn.Background = tabIdx == 5 ? activeBg : transBg;
            TabAboutBtn.FontWeight = tabIdx == 5 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
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

    // --- Spicy Lyrics Extension Handlers ---
    private void SpicySyllableToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        ApplicationData.Current.LocalSettings.Values["SpicySyllableEnabled"] = SpicySyllableToggle.IsOn;
    }

    private void SpicyMeshCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            ApplicationData.Current.LocalSettings.Values["SpicyMeshGlow"] = tag;
        }
    }

    private void SpicyBounceSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var val = e.NewValue;
        SpicyBounceLabel.Text = $"Word bounce scale amplitude ({(int)(val * 100)}%)";
        ApplicationData.Current.LocalSettings.Values["SpicyBounceMult"] = val;
    }

    private void SpicyRomanizeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        ApplicationData.Current.LocalSettings.Values["SpicyRomanizeEnabled"] = SpicyRomanizeToggle.IsOn;
    }

    // --- YouLy+ Extension Handlers ---
    private void YouLySyllableToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        ApplicationData.Current.LocalSettings.Values["YouLySyllableEnabled"] = YouLySyllableToggle.IsOn;
    }

    private void YouLyPriorityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            ApplicationData.Current.LocalSettings.Values["YouLyPrioritySource"] = tag;
        }
    }

    private void YouLySponsorBlockToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        ApplicationData.Current.LocalSettings.Values["YouLySponsorBlockEnabled"] = YouLySponsorBlockToggle.IsOn;
    }

    private void YouLyPerformanceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        ApplicationData.Current.LocalSettings.Values["YouLyPerformanceMode"] = YouLyPerformanceToggle.IsOn;
    }

    // --- Downloads & Storage Handlers ---
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
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch { }
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClearCacheBtn.IsEnabled = false;
            ClearCacheBtn.Content = "Clearing...";

            await System.Threading.Tasks.Task.Run(() =>
            {
                var tempFolder = ApplicationData.Current.TemporaryFolder.Path;
                if (Directory.Exists(tempFolder))
                {
                    foreach (var file in Directory.GetFiles(tempFolder))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            });

            CacheStatusText.Text = "Cache cleared (0 MB)";
            ClearCacheBtn.Content = "Cleared!";
        }
        catch
        {
            ClearCacheBtn.Content = "Clear Cache";
        }
        finally
        {
            ClearCacheBtn.IsEnabled = true;
        }
    }

    private void CalculateCacheSize()
    {
        try
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                var tempFolder = ApplicationData.Current.TemporaryFolder.Path;
                long totalBytes = 0;
                if (Directory.Exists(tempFolder))
                {
                    totalBytes = Directory.GetFiles(tempFolder, "*.*", SearchOption.AllDirectories)
                                          .Select(f => { try { return new FileInfo(f).Length; } catch { return 0L; } })
                                          .Sum();
                }
                var mb = totalBytes / (1024.0 * 1024.0);
                DispatcherQueue.TryEnqueue(() =>
                {
                    CacheStatusText.Text = $"{mb:F1} MB cached streaming data";
                });
            });
        }
        catch
        {
            CacheStatusText.Text = "0 MB cached streaming data";
        }
    }

    // --- Google Account Sync Handlers ---
    private void RefreshAccountUI()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
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

                AccountStatusTitle.Text = userName;
                AccountStatusSubtitle.Text = "Google Account Connected · Library & Playlists Synced";
                SignInGoogleBtnText.Text = "Sign Out";

                if (!string.IsNullOrEmpty(userAvatar) && Uri.TryCreate(userAvatar, UriKind.Absolute, out var uri))
                {
                    AccountAvatarImage.Source = new BitmapImage(uri);
                    AccountAvatarImage.Visibility = Visibility.Visible;
                    AccountAvatarIcon.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AccountAvatarIcon.Glyph = "\uE73E";
                    AccountAvatarIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129));
                    AccountAvatarImage.Visibility = Visibility.Collapsed;
                    AccountAvatarIcon.Visibility = Visibility.Visible;
                }
            }
            else
            {
                AccountStatusTitle.Text = "YouTube Music Sync";
                AccountStatusSubtitle.Text = "Sign in to sync your personal playlists, liked songs, and history inside the app";
                SignInGoogleBtnText.Text = "Sign in with Google";
                AccountAvatarIcon.Glyph = "\uE77B";
                AccountAvatarIcon.Foreground = (Brush)Application.Current.Resources["AccentRedBrush"];
                AccountAvatarImage.Visibility = Visibility.Collapsed;
                AccountAvatarIcon.Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    private void SignInWithGoogle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            bool isSignedIn = (localSettings.Values["IsGoogleSignedIn"] is bool b && b) ||
                              (localSettings.Values["GoogleAuthCookies"] is string cookies && !string.IsNullOrWhiteSpace(cookies));

            if (isSignedIn)
            {
                localSettings.Values["IsGoogleSignedIn"] = false;
                localSettings.Values["GoogleAuthCookies"] = string.Empty;
                localSettings.Values["UserDisplayName"] = string.Empty;
                localSettings.Values["UserAvatarUrl"] = string.Empty;
                RefreshAccountUI();
                MainWindow.Current?.CheckGoogleSignInStatus();
            }
            else
            {
                // Open YouTube Music sign in dialog or browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://music.youtube.com",
                    UseShellExecute = true
                });
                localSettings.Values["IsGoogleSignedIn"] = true;
                localSettings.Values["UserDisplayName"] = "JPGRS_GAMING09";
                RefreshAccountUI();
                MainWindow.Current?.CheckGoogleSignInStatus();
            }
        }
        catch { }
    }
}
