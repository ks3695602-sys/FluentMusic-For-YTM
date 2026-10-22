using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        if (ViewModel.Shelves.Count == 0 && ViewModel.TopResults.Count == 0)
        {
            await ViewModel.LoadPersonalizedFeedAsync(null);
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

    private async void MoodChip_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MoodChip chip)
        {
            ViewModel.ActiveMoodTitle = chip.Title;
            await ViewModel.LoadPersonalizedFeedAsync(!string.IsNullOrWhiteSpace(chip.Param) ? chip.Param : null);
        }
    }

    private async void MoodChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            string param = btn.Tag?.ToString() ?? string.Empty;
            string title = btn.Content?.ToString() ?? string.Empty;
            ViewModel.ActiveMoodTitle = title;
            await ViewModel.LoadPersonalizedFeedAsync(!string.IsNullOrWhiteSpace(param) ? param : null);
        }
    }

    private async void ShelfItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResult result)
        {
            await ViewModel.PlaySongAsync(result);
        }
    }

    private async void PlayShelf_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is HomeShelf shelf)
        {
            await ViewModel.PlayShelfAsync(shelf);
        }
    }
}
