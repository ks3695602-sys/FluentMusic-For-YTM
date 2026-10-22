using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

        // Keyboard shortcut: Ctrl+E or Ctrl+F focuses search box
        this.Loaded += (s, e) =>
        {
            if (this.XamlRoot?.Content is UIElement rootElement)
            {
                rootElement.KeyDown -= OnRootKeyDown;
                rootElement.KeyDown += OnRootKeyDown;
            }
        };
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            if (e.Key == Windows.System.VirtualKey.E || e.Key == Windows.System.VirtualKey.F)
            {
                SearchBox.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.TopResults.Count == 0)
        {
            await ViewModel.LoadTrendingCommand.ExecuteAsync(null);
        }
    }

    private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = args.QueryText;
        if (!string.IsNullOrWhiteSpace(query))
        {
            ViewModel.SearchQuery = query;
            await ViewModel.SearchCommand.ExecuteAsync(query);
        }
    }

    private async void MoodChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string moodQuery)
        {
            SearchBox.Text = moodQuery;
            ViewModel.SearchQuery = moodQuery;
            await ViewModel.SearchCommand.ExecuteAsync(moodQuery);
        }
    }

    private void HeroCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = 1.04;
            scale.ScaleY = 1.04;
        }
    }

    private void HeroCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = 1.0;
            scale.ScaleY = 1.0;
        }
    }

    private async void HeroCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string query)
        {
            SearchBox.Text = query;
            ViewModel.SearchQuery = query;
            await ViewModel.SearchCommand.ExecuteAsync(query);
        }
    }

    private async void TrackCard_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(result);
        }
    }

    private async void PlayFeatured_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TopResults.Count > 0)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(ViewModel.TopResults[0]);
        }
        else if (ViewModel.TrendingSongs.Count > 0)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(ViewModel.TrendingSongs[0]);
        }
    }

    private async void MenuPlayNow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is SearchResult result)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(result);
        }
    }

    private void MenuPlayNext_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is SearchResult result)
        {
            ViewModel.PlayNext(result);
        }
    }

    private void MenuAddToQueue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is SearchResult result)
        {
            ViewModel.AddToQueue(result);
        }
    }
}
