using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Controls;

public sealed partial class PlayerBar : UserControl
{
    public PlayerBarViewModel ViewModel { get; }

    public event EventHandler? LyricsToggled;
    public event EventHandler? NowPlayingRequested;
    public event EventHandler? NowPlayingSidebarToggled;

    public PlayerBar()
    {
        ViewModel = App.Services.GetRequiredService<PlayerBarViewModel>();
        this.InitializeComponent();
    }

    private void LyricsButton_Click(object sender, RoutedEventArgs e)
    {
        LyricsToggled?.Invoke(this, EventArgs.Empty);
    }

    private void NowPlaying_Click(object sender, RoutedEventArgs e)
    {
        NowPlayingRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NowPlayingSidebar_Click(object sender, RoutedEventArgs e)
    {
        NowPlayingSidebarToggled?.Invoke(this, EventArgs.Empty);
    }

    public void OpenQueueFlyout()
    {
        try
        {
            PlayerQueueBtn?.Flyout?.ShowAt(PlayerQueueBtn);
        }
        catch { }
    }

    private void SpeedItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string speedStr)
        {
            ViewModel.SetSpeedCommand.Execute(speedStr);
        }
    }

    private void CenterLcdPill_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        NowPlayingRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearQueue_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PlaybackService.UpNext.Clear();
    }

    private async void QueueItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Models.Song song)
        {
            await ViewModel.PlaybackService.PlayAsync(song);
        }
    }

    private void RemoveQueueItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string videoId)
        {
            var match = System.Linq.Enumerable.FirstOrDefault(ViewModel.PlaybackService.UpNext, s => s.VideoId == videoId);
            if (match != null)
            {
                ViewModel.PlaybackService.UpNext.Remove(match);
            }
        }
    }
}
