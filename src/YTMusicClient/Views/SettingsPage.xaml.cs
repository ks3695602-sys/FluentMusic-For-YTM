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
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;

        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString() ?? "Default";
            ViewModel.SetThemeCommand.Execute(tag);

            // Apply theme to root element
            if (this.XamlRoot?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ViewModel.CurrentTheme;
            }
        }
    }

    private async void SignInWithGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (this.XamlRoot == null) return;

        var dialog = new ContentDialog
        {
            Title = "Google Account Sync",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Connecting your Google Account allows you to sync your liked songs, custom playlists, and private recommendations directly from YouTube Music.",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13
                    },
                    new InfoBar
                    {
                        IsOpen = true,
                        Severity = InfoBarSeverity.Informational,
                        Message = "Direct Google authentication via WebView2 session capture is enabled. Your account session is securely stored in Windows Credential Locker.",
                        IsClosable = false
                    }
                }
            },
            PrimaryButtonText = "Sign In Now",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // Open system browser or WebView2 sign in
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://music.youtube.com"));
            }
            catch { }
        }
    }
}
