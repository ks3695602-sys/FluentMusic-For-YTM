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
}
