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

            if (this.XamlRoot?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ViewModel.CurrentTheme;
            }
        }
    }

    private async void SignInWithGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (this.XamlRoot == null) return;

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

            webView.Source = new Uri("https://accounts.google.com/ServiceLogin?continue=https://music.youtube.com");

            await dialog.ShowAsync();
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
