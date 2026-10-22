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
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
        this.InitializeComponent();
        this.Loaded += async (s, e) => await ViewModel.LoadLibraryAsync();
    }

    private async void LikedSong_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Song song)
        {
            await ViewModel.PlaySongCommand.ExecuteAsync(song);
        }
    }
}
