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

    private async void AddLocalSongs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".flac");
            picker.FileTypeFilter.Add(".m4a");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".aac");
            picker.FileTypeFilter.Add(".opus");

            var window = App.MainWindow;
            if (window != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                var paths = files.Select(f => f.Path).ToList();
                await ViewModel.ImportLocalFilesAsync(paths);
            }
        }
        catch (System.Exception ex)
        {
            Helpers.AppLogger.LogError("AddLocalSongs_Click failed", ex);
        }
    }
}
