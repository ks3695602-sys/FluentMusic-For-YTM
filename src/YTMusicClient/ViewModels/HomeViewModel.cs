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
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _hasSearchText;

    [ObservableProperty]
    private bool _hasPersonalizedShelves;

    [ObservableProperty]
    private string _activeMoodTitle = "All (सब)";

    public ObservableCollection<MoodChip> MoodChips { get; } = new();
    public ObservableCollection<HomeShelf> Shelves { get; } = new();

    public ObservableCollection<SearchResult> TopResults { get; } = new();
    public ObservableCollection<SearchResult> TopArtists { get; } = new();
    public ObservableCollection<SearchResult> TrendingSongs { get; } = new();

    public HomeViewModel(IYouTubeMusicService musicService, IMediaPlaybackService playbackService)
    {
        _musicService = musicService;
        _playbackService = playbackService;
        UpdateGreeting();
    }

    partial void OnSearchQueryChanged(string value)
    {
        HasSearchText = !string.IsNullOrWhiteSpace(value);
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
        if (string.IsNullOrWhiteSpace(targetQuery))
        {
            await LoadTrendingAsync();
            return;
        }

        IsLoading = true;
        try
        {
            AppLogger.Log($"HomeViewModel.SearchAsync for: '{targetQuery}'");
            var results = await _musicService.SearchAsync(targetQuery);

            TopResults.Clear();
            TrendingSongs.Clear();
            TopArtists.Clear();
            HasPersonalizedShelves = false;

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
    public async Task ClearSearchAsync()
    {
        SearchQuery = string.Empty;
        await LoadTrendingAsync();
    }

    [RelayCommand]
    public async Task LoadTrendingAsync()
    {
        await LoadPersonalizedFeedAsync(null);
    }

    public async Task LoadPersonalizedFeedAsync(string? moodParam = null)
    {
        IsLoading = true;
        try
        {
            var feed = await _musicService.GetPersonalizedHomeFeedAsync(moodParam);

            // Update Mood Chips if returned
            if (feed.Chips.Count > 0)
            {
                MoodChips.Clear();
                foreach (var c in feed.Chips)
                {
                    MoodChips.Add(c);
                }
            }

            // Update Shelves
            if (feed.Shelves.Count > 0)
            {
                Shelves.Clear();
                foreach (var s in feed.Shelves)
                {
                    Shelves.Add(s);
                }
                HasPersonalizedShelves = true;
            }
            else
            {
                // Fallback to trending tracks
                HasPersonalizedShelves = false;
                var results = await _musicService.GetTrendingAsync();

                TopResults.Clear();
                TrendingSongs.Clear();
                TopArtists.Clear();

                foreach (var r in results.Take(6))
                {
                    TopResults.Add(r);
                }

                foreach (var r in results.Skip(6))
                {
                    TrendingSongs.Add(r);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("LoadPersonalizedFeedAsync failed", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PlaySongAsync(SearchResult? result)
    {
        if (result == null) return;

        AppLogger.Log($"PlaySongAsync invoked for: {result.Title} ({result.VideoId})");

        var song = new Song
        {
            VideoId = result.VideoId,
            Title = result.Title,
            Artist = result.Artist,
            Album = result.Album,
            ThumbnailUrl = result.ThumbnailUrl,
            Duration = result.Duration,
            GradientColorStart = result.GradientStart,
            GradientColorEnd = result.GradientEnd,
            AudioQuality = result.AudioQuality,
            FidelityTier = result.FidelityTier
        };

        await _playbackService.PlayAsync(song);
    }

    [RelayCommand]
    public async Task PlayShelfAsync(HomeShelf? shelf)
    {
        if (shelf == null || shelf.Items.Count == 0) return;

        var first = shelf.Items[0];
        await PlaySongAsync(first);

        // Add remaining tracks to queue
        foreach (var item in shelf.Items.Skip(1))
        {
            _playbackService.UpNext.Add(new Song
            {
                VideoId = item.VideoId,
                Title = item.Title,
                Artist = item.Artist,
                Album = item.Album,
                ThumbnailUrl = item.ThumbnailUrl,
                Duration = item.Duration,
                GradientColorStart = item.GradientStart,
                GradientColorEnd = item.GradientEnd,
                AudioQuality = item.AudioQuality,
                FidelityTier = item.FidelityTier
            });
        }
    }
}
