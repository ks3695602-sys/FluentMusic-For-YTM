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
}
