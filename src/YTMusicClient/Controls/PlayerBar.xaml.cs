using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using YTMusicClient.Services;
using YTMusicClient.ViewModels;
using YTMusicClient.Views;

namespace YTMusicClient.Controls;

public sealed partial class PlayerBar : UserControl
{
    public PlayerBarViewModel ViewModel { get; }

    public event EventHandler? LyricsToggled;
    public event EventHandler? QueueToggled;

    public PlayerBar()
    {
        ViewModel = App.Services.GetRequiredService<PlayerBarViewModel>();
        this.InitializeComponent();
    }

    private void CenterLcdPill_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var navService = App.Services.GetService(typeof(INavigationService)) as INavigationService;
        navService?.NavigateTo(typeof(NowPlayingPage));
    }

    private void OpenFullScreen_Click(object sender, RoutedEventArgs e)
    {
        var navService = App.Services.GetService(typeof(INavigationService)) as INavigationService;
        navService?.NavigateTo(typeof(NowPlayingPage));
    }

    private void LyricsButton_Click(object sender, RoutedEventArgs e)
    {
        LyricsToggled?.Invoke(this, EventArgs.Empty);
    }

    private void QueueButton_Click(object sender, RoutedEventArgs e)
    {
        QueueToggled?.Invoke(this, EventArgs.Empty);
    }

    private void SpeedOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string speedStr)
        {
            ViewModel.SetSpeedCommand.Execute(speedStr);
        }
    }

    private void RepeatOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && int.TryParse(item.Tag?.ToString(), out var mode))
        {
            ViewModel.RepeatMode = mode;
            ViewModel.PlaybackService.RepeatMode = mode;
        }
    }

    private void CopyStreamUrl_Click(object sender, RoutedEventArgs e)
    {
        var current = ViewModel.PlaybackService.CurrentSong;
        if (current != null && !string.IsNullOrEmpty(current.StreamUrl))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(current.StreamUrl);
            Clipboard.SetContent(dataPackage);
        }
    }
}
