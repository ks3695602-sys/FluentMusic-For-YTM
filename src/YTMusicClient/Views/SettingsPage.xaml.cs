using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private void RefreshAccountUI()
    {
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        bool isSignedIn = localSettings.Values["IsGoogleSignedIn"] is bool b && b;
        if (isSignedIn)
        {
            var user = localSettings.Values["UserDisplayName"] as string ?? "Google User";
            AccountStatusTitle.Text = $"Connected: {user}";
            AccountStatusSubtitle.Text = "Your YouTube Music playlists and liked songs are synced with FluentMusic.";
            SignInGoogleBtnText.Text = "Sign Out";
        }
        else
        {
            AccountStatusTitle.Text = "YouTube Music Sync";
            AccountStatusSubtitle.Text = "Sign in to sync your personal playlists, liked songs, and history inside the app";
            SignInGoogleBtnText.Text = "Sign in with Google";
        }
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
            RefreshAccountUI();

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
                Width = 460,
                Height = 560,
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
                                try
                                {
                                    var scriptResult = await webView.ExecuteScriptAsync("document.querySelector('button#avatar-btn')?.getAttribute('aria-label') || ''");
                                    if (!string.IsNullOrWhiteSpace(scriptResult) && scriptResult != "null" && scriptResult != "\"\"")
                                    {
                                        displayName = scriptResult.Trim('"', ' ');
                                    }
                                }
                                catch { }

                                localSettings.Values["UserDisplayName"] = displayName;

                                var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                                localSettings.Values["GoogleAuthCookies"] = cookieHeader;

                                RefreshAccountUI();
                                dialog.Hide();

                                var successDialog = new ContentDialog
                                {
                                    Title = "Signed In Successfully",
                                    Content = $"Connected as {displayName}. Your YouTube Music personal library is now synced with FluentMusic!",
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
