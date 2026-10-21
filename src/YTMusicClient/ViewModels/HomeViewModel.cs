using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Helpers;
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

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public ObservableCollection<SearchResult> TrendingSongs { get; } = new();
    public ObservableCollection<SearchResult> RecentlyPlayed { get; } = new();
    public ObservableCollection<SearchResult> SearchSuggestions { get; } = new();

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

        AppLogger.Log($"PlaySongAsync invoked for: {result.Title} ({result.VideoId})");

        // 1. Instant optimistic UI feedback: show song in bottom player bar immediately
        var song = new Song
        {
            VideoId = result.VideoId,
            Title = result.Title,
            Artist = result.Artist,
            ThumbnailUrl = result.ThumbnailUrl,
            Duration = result.Duration,
            StreamUrl = string.Empty
        };

        _playbackService.SetCurrentSong(song);

        // 2. Fetch audio stream URL with fallback engine
        try
        {
            var streamUrl = await _musicService.GetAudioStreamUrlAsync(result.VideoId);
            if (!string.IsNullOrEmpty(streamUrl))
            {
                song.StreamUrl = streamUrl;
                await _playbackService.PlayAsync(song);
            }
            else
            {
                AppLogger.Log($"PlaySongAsync: No stream available for {result.VideoId}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"PlaySongAsync failed for {result.VideoId}", ex);
        }
    }
}
