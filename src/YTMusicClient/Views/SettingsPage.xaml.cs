using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        this.InitializeComponent();
        this.Loaded += (s, e) => RefreshAccountUI();
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int tabIdx))
        {
            SectionAppearance.Visibility = tabIdx == 0 ? Visibility.Visible : Visibility.Collapsed;
            SectionAudio.Visibility = tabIdx == 1 ? Visibility.Visible : Visibility.Collapsed;
            SectionAccount.Visibility = tabIdx == 2 ? Visibility.Visible : Visibility.Collapsed;
            SectionDownloads.Visibility = tabIdx == 3 ? Visibility.Visible : Visibility.Collapsed;
            SectionAbout.Visibility = tabIdx == 4 ? Visibility.Visible : Visibility.Collapsed;

            var activeBg = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            var transBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            TabAppearanceBtn.Background = tabIdx == 0 ? activeBg : transBg;
            TabAppearanceBtn.FontWeight = tabIdx == 0 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabAudioBtn.Background = tabIdx == 1 ? activeBg : transBg;
            TabAudioBtn.FontWeight = tabIdx == 1 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabAccountBtn.Background = tabIdx == 2 ? activeBg : transBg;
            TabAccountBtn.FontWeight = tabIdx == 2 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabDownloadsBtn.Background = tabIdx == 3 ? activeBg : transBg;
            TabDownloadsBtn.FontWeight = tabIdx == 3 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabAboutBtn.Background = tabIdx == 4 ? activeBg : transBg;
            TabAboutBtn.FontWeight = tabIdx == 4 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private void RefreshAccountUI()
    {
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
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

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;

        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString() ?? "Default";
            ViewModel.SetThemeCommand.Execute(tag);

            if (this.XamlRoot?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ViewModel.CurrentTheme;
            }
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

    private async void SignInWithGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (this.XamlRoot == null) return;

        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        bool isSignedIn = localSettings.Values["IsGoogleSignedIn"] is bool b && b;

        // If already signed in, handle sign out
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

            // Intercept navigation to capture cookies after authentication
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
