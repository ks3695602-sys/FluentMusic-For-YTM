using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using YTMusicClient.Models;
using YTMusicClient.Services;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        ViewModel = App.Services.GetRequiredService<HomeViewModel>();
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.TopResults.Count == 0)
        {
            await ViewModel.LoadTrendingCommand.ExecuteAsync(null);
        }
    }

    private async void SearchInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.SearchQuery = SearchInputBox.Text;
            await ViewModel.SearchCommand.ExecuteAsync(SearchInputBox.Text);
        }
    }

    private async void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        SearchInputBox.Text = string.Empty;
        await ViewModel.ClearSearchCommand.ExecuteAsync(null);
    }

    private async void TrackCard_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(result);
        }
    }

    private async void ArtistItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
        {
            SearchInputBox.Text = result.Artist;
            await ViewModel.SearchCommand.ExecuteAsync(result.Artist);
        }
    }

    private void LibraryShortcut_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(LibraryPage));
    }

    private async void MenuPlayNow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is SearchResult result)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(result);
        }
    }

    private void MenuPlayNext_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is SearchResult result)
        {
            ViewModel.PlayNextCommand.Execute(result);
        }
    }

    private void MenuAddToQueue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is SearchResult result)
        {
            ViewModel.AddToQueueCommand.Execute(result);
        }
    }

    private async void MenuGoToArtist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is SearchResult result)
        {
            SearchInputBox.Text = result.Artist;
            await ViewModel.SearchCommand.ExecuteAsync(result.Artist);
        }
    }

    private void MenuCopyLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is SearchResult result && !string.IsNullOrEmpty(result.VideoId))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText($"https://music.youtube.com/watch?v={result.VideoId}");
            Clipboard.SetContent(dataPackage);
        }
    }
}
