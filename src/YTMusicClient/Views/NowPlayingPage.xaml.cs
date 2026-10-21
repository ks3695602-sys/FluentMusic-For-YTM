using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using YTMusicClient.Models;
using YTMusicClient.Services;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class NowPlayingPage : Page
{
    public PlayerBarViewModel PlayerViewModel { get; }
    public LyricsViewModel LyricsViewModel { get; }

    public NowPlayingPage()
    {
        PlayerViewModel = App.Services.GetRequiredService<PlayerBarViewModel>();
        LyricsViewModel = App.Services.GetRequiredService<LyricsViewModel>();
        this.InitializeComponent();

        LyricsViewModel.ActiveLyricChanged += OnActiveLyricChanged;
    }

    private void OnActiveLyricChanged(object? sender, int activeIndex)
    {
        if (activeIndex >= 0 && activeIndex < LyricsViewModel.Lyrics.Count)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    NowPlayingLyricsList.ScrollIntoView(LyricsViewModel.Lyrics[activeIndex], ScrollIntoViewAlignment.Leading);
                }
                catch { }
            });
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(HomePage));
        }
    }

    private void SongModeBtn_Click(object sender, RoutedEventArgs e)
    {
        SongStageContainer.Visibility = Visibility.Visible;
        VideoStageContainer.Visibility = Visibility.Collapsed;

        SongModeBtn.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        VideoModeBtn.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private async void VideoModeBtn_Click(object sender, RoutedEventArgs e)
    {
        SongStageContainer.Visibility = Visibility.Collapsed;
        VideoStageContainer.Visibility = Visibility.Visible;

        VideoModeBtn.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        SongModeBtn.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

        try
        {
            var currentSong = PlayerViewModel.PlaybackService.CurrentSong;
            if (currentSong != null && !string.IsNullOrEmpty(currentSong.VideoId))
            {
                await VideoPlayerWebView.EnsureCoreWebView2Async();
                var embedUrl = $"https://www.youtube.com/embed/{currentSong.VideoId}?autoplay=1&controls=1";
                VideoPlayerWebView.Source = new Uri(embedUrl);
            }
        }
        catch { }
    }

    private void LyricLine_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LyricLine line)
        {
            LyricsViewModel.SeekToLineCommand.Execute(line);
        }
    }

    private void PlayNext_Click(object sender, RoutedEventArgs e)
    {
        var current = PlayerViewModel.PlaybackService.CurrentSong;
        if (current != null)
        {
            PlayerViewModel.PlaybackService.EnqueueNext(current);
        }
    }

    private void AddToQueue_Click(object sender, RoutedEventArgs e)
    {
        var current = PlayerViewModel.PlaybackService.CurrentSong;
        if (current != null)
        {
            PlayerViewModel.PlaybackService.Enqueue(current);
        }
    }

    private void Speed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string speedStr)
        {
            PlayerViewModel.SetSpeedCommand.Execute(speedStr);
        }
    }

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        var current = PlayerViewModel.PlaybackService.CurrentSong;
        if (current != null && !string.IsNullOrEmpty(current.VideoId))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText($"https://music.youtube.com/watch?v={current.VideoId}");
            Clipboard.SetContent(dataPackage);
        }
    }
}
