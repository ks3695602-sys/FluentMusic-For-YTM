using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
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
            await ViewModel.SearchCommand.ExecuteAsync(null);
        }
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
            ViewModel.SearchQuery = result.Artist;
            await ViewModel.SearchCommand.ExecuteAsync(result.Artist);
        }
    }
}
