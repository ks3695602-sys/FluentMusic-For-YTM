using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
    private string _greeting = "Good Evening";

    [ObservableProperty]
    private string _searchQuery = "nights off";

    [ObservableProperty]
    private bool _isAppleMusicSource = true;

    public ObservableCollection<SearchResult> TopResults { get; } = new();
    public ObservableCollection<SearchResult> TopArtists { get; } = new();
    public ObservableCollection<SearchResult> TrendingSongs { get; } = new();

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
    public async Task SearchAsync(string? query = null)
    {
        var targetQuery = !string.IsNullOrWhiteSpace(query) ? query : SearchQuery;
        if (string.IsNullOrWhiteSpace(targetQuery)) return;

        IsLoading = true;
        try
        {
            AppLogger.Log($"HomeViewModel.SearchAsync for: '{targetQuery}'");
            var results = await _musicService.SearchAsync(targetQuery);

            TopResults.Clear();
            TrendingSongs.Clear();
            TopArtists.Clear();

            // First 6 tracks for Top Results 2x3 grid
            foreach (var r in results.Take(6))
            {
                TopResults.Add(r);
            }

            // Next tracks for albums/songs grid
            foreach (var r in results.Skip(6))
            {
                TrendingSongs.Add(r);
            }

            // Unique artists for Circular Artist Row
            var distinctArtists = results
                .GroupBy(r => r.Artist)
                .Select(g => g.First())
                .Take(6);

            foreach (var a in distinctArtists)
            {
                TopArtists.Add(a);
            }

            AppLogger.Log($"SearchAsync populated: TopResults={TopResults.Count}, Artists={TopArtists.Count}, Trending={TrendingSongs.Count}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadTrendingAsync()
    {
        if (TopResults.Count > 0) return;
        await SearchAsync("Top Hits 2026");
    }

    [RelayCommand]
    public async Task PlaySongAsync(SearchResult? result)
    {
        if (result == null) return;

        AppLogger.Log($"PlaySongAsync invoked for: {result.Title} ({result.VideoId})");

        // Instant optimistic feedback in floating player
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

        // Fetch streaming audio from BravePipe / YouTube / Fallback
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
