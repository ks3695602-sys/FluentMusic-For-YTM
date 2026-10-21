using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using YTMusicClient.Models;
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
        if (ViewModel.TrendingSongs.Count == 0)
        {
            await ViewModel.LoadTrendingCommand.ExecuteAsync(null);
        }
    }

    private void HomeSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            var exploreVm = App.Services.GetRequiredService<ExploreViewModel>();
            exploreVm.SearchQuery = args.QueryText;
            exploreVm.SearchCommand.Execute(null);

            var navService = App.Services.GetRequiredService<INavigationService>();
            navService.NavigateTo(typeof(ExplorePage));
        }
    }

    private async void TrendingItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(result);
        }
    }
}
