using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IYouTubeMusicService _musicService;
    private readonly IMediaPlaybackService _playbackService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _greeting = "Good Morning";

    public ObservableCollection<SearchResult> TrendingSongs { get; } = new();
    public ObservableCollection<SearchResult> RecentlyPlayed { get; } = new();

    public HomeViewModel(IYouTubeMusicService musicService, IMediaPlaybackService playbackService)
    {
        _musicService = musicService;
        _playbackService = playbackService;
        UpdateGreeting();
    }

    private void UpdateGreeting()
    {
        var hour = DateTime.Now.Hour;
        Greeting = hour switch
        {
            < 12 => "Good Morning",
            < 17 => "Good Afternoon",
            _ => "Good Evening"
        };
    }

    [RelayCommand]
    private async Task LoadTrendingAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            TrendingSongs.Clear();
            var results = await _musicService.GetTrendingAsync();
            foreach (var r in results)
                TrendingSongs.Add(r);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlaySongAsync(SearchResult? result)
    {
        if (result == null) return;

        try
        {
            var song = await _musicService.GetSongDetailsAsync(result.VideoId);
            await _playbackService.PlayAsync(song);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PlaySong failed: {ex.Message}");
        }
    }
}
