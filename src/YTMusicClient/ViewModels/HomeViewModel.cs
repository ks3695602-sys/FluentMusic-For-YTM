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
        IsLoading = true;
        try
        {
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

            var distinctArtists = results
                .GroupBy(r => r.Artist)
                .Select(g => g.First())
                .Take(6);

            foreach (var a in distinctArtists)
            {
                TopArtists.Add(a);
            }
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
            AudioQuality = result.AudioQuality,
            GradientColorStart = result.GradientStart,
            GradientColorEnd = result.GradientEnd
        };

        // Contextual Auto-Queue: Queue subsequent songs in TopResults
        var currentIndex = TopResults.IndexOf(result);
        if (currentIndex >= 0)
        {
            _playbackService.ClearQueue();
            for (int i = currentIndex + 1; i < TopResults.Count; i++)
            {
                var next = TopResults[i];
                _playbackService.Enqueue(new Song
                {
                    VideoId = next.VideoId,
                    Title = next.Title,
                    Artist = next.Artist,
                    ThumbnailUrl = next.ThumbnailUrl,
                    Duration = next.Duration,
                    AudioQuality = next.AudioQuality,
                    GradientColorStart = next.GradientStart,
                    GradientColorEnd = next.GradientEnd
                });
            }
        }

        // Set optimistic display in PlayerBar immediately
        _playbackService.SetCurrentSong(song);

        // Fetch streaming audio URL and begin playing
        var streamUrl = await _musicService.GetAudioStreamUrlAsync(result.VideoId);
        song.StreamUrl = streamUrl;

        await _playbackService.PlayAsync(song);
    }

    [RelayCommand]
    public void PlayNext(SearchResult? result)
    {
        if (result == null) return;
        _playbackService.EnqueueNext(new Song
        {
            VideoId = result.VideoId,
            Title = result.Title,
            Artist = result.Artist,
            ThumbnailUrl = result.ThumbnailUrl,
            Duration = result.Duration,
            AudioQuality = result.AudioQuality,
            GradientColorStart = result.GradientStart,
            GradientColorEnd = result.GradientEnd
        });
    }

    [RelayCommand]
    public void AddToQueue(SearchResult? result)
    {
        if (result == null) return;
        _playbackService.Enqueue(new Song
        {
            VideoId = result.VideoId,
            Title = result.Title,
            Artist = result.Artist,
            ThumbnailUrl = result.ThumbnailUrl,
            Duration = result.Duration,
            AudioQuality = result.AudioQuality,
            GradientColorStart = result.GradientStart,
            GradientColorEnd = result.GradientEnd
        });
    }
}
