using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using YTMusicClient.Models;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class ExplorePage : Page
{
    public ExploreViewModel ViewModel { get; }

    public ExplorePage()
    {
        ViewModel = App.Services.GetRequiredService<ExploreViewModel>();
        this.InitializeComponent();
    }

    private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.SearchQuery = args.QueryText;
        await ViewModel.SearchCommand.ExecuteAsync(null);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.SearchQuery = sender.Text;
        }
    }

    private async void ResultItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(result);
        }
    }

    private async void MenuPlayNow_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement el && el.DataContext is SearchResult item)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(item);
        }
    }

    private void MenuPlayNext_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement el && el.DataContext is SearchResult item)
        {
            var playback = App.Services.GetService(typeof(Services.IMediaPlaybackService)) as Services.IMediaPlaybackService;
            playback?.EnqueueNext(new Song
            {
                VideoId = item.VideoId,
                Title = item.Title,
                Artist = item.Artist,
                ThumbnailUrl = item.ThumbnailUrl,
                Duration = item.Duration
            });
        }
    }

    private void MenuAddToQueue_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement el && el.DataContext is SearchResult item)
        {
            var playback = App.Services.GetService(typeof(Services.IMediaPlaybackService)) as Services.IMediaPlaybackService;
            playback?.Enqueue(new Song
            {
                VideoId = item.VideoId,
                Title = item.Title,
                Artist = item.Artist,
                ThumbnailUrl = item.ThumbnailUrl,
                Duration = item.Duration
            });
        }
    }

    private async void MenuDownload_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement el && el.DataContext is SearchResult item)
        {
            var downloadService = App.Services.GetService(typeof(Services.IDownloadService)) as Services.IDownloadService;
            if (downloadService != null)
            {
                var song = new Song
                {
                    VideoId = item.VideoId,
                    Title = item.Title,
                    Artist = item.Artist,
                    ThumbnailUrl = item.ThumbnailUrl,
                    Duration = item.Duration
                };
                await downloadService.DownloadSongAsync(song);
            }
        }
    }
}
