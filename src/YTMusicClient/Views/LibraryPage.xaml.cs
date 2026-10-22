using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using YTMusicClient.Models;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
        this.InitializeComponent();
        this.Loaded += async (s, e) => await ViewModel.LoadLibraryAsync();
    }

    private void PlaylistsTab_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 0;
        PlaylistsView.Visibility = ViewModel.IsPlaylistDetailOpen ? Visibility.Collapsed : Visibility.Visible;
        PlaylistDetailView.Visibility = ViewModel.IsPlaylistDetailOpen ? Visibility.Visible : Visibility.Collapsed;
        LikedSongsView.Visibility = Visibility.Collapsed;

        PlaylistsTabBtn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        LikedSongsTabBtn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void LikedSongsTab_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 1;
        PlaylistsView.Visibility = Visibility.Collapsed;
        PlaylistDetailView.Visibility = Visibility.Collapsed;
        LikedSongsView.Visibility = Visibility.Visible;

        LikedSongsTabBtn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        PlaylistsTabBtn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private async void Playlist_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Playlist playlist)
        {
            await ViewModel.OpenPlaylistCommand.ExecuteAsync(playlist);
            PlaylistsView.Visibility = Visibility.Collapsed;
            PlaylistDetailView.Visibility = Visibility.Visible;
        }
    }

    private async void PlaylistSong_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Song song)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }

    private async void LikedSong_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Song song)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }
}
