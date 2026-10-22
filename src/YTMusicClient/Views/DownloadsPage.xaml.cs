using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using YTMusicClient.Models;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class DownloadsPage : Page
{
    public DownloadsViewModel ViewModel { get; }

    public DownloadsPage()
    {
        ViewModel = App.Services.GetRequiredService<DownloadsViewModel>();
        this.InitializeComponent();
        this.Loaded += async (s, e) => await ViewModel.LoadDownloadsAsync();
    }

    private async void DownloadedSong_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Song song)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }

    private async void DeleteSong_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string videoId)
        {
            var song = ViewModel.DownloadedSongs.FirstOrDefault(s => s.VideoId == videoId);
            if (song != null)
            {
                await ViewModel.DeleteSongCommand.ExecuteAsync(song);
            }
        }
    }
}
