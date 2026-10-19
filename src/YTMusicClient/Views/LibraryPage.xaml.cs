using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using YTMusicClient.Models;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
    }

    private async void LikedSong_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Song song)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }
}
