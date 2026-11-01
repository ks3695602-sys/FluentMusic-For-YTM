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

    [ObservableProperty]
    private bool _isUserSignedIn;

    [ObservableProperty]
    private string _userDisplayName = "Guest";

    [ObservableProperty]
    private string _userAvatarUrl = string.Empty;

    [ObservableProperty]
    private bool _hasQuickPicks;

    public string AccountStatusDisplay => IsUserSignedIn ? $"● Synced: {UserDisplayName}" : "● Guest Mode";

    public ObservableCollection<MoodChip> MoodChips { get; } = new();
    public ObservableCollection<HomeShelf> Shelves { get; } = new();
    public ObservableCollection<SearchResult> QuickPicks { get; } = new();

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

    public void UpdateGreeting()
    {
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var cookies = localSettings.Values["GoogleAuthCookies"] as string;
            var name = localSettings.Values["UserDisplayName"] as string;
            var avatar = localSettings.Values["UserAvatarUrl"] as string;

            IsUserSignedIn = !string.IsNullOrWhiteSpace(cookies);
            UserDisplayName = !string.IsNullOrWhiteSpace(name) ? name : (IsUserSignedIn ? "Signed In" : "Guest");
            UserAvatarUrl = avatar ?? string.Empty;
        }
        catch { }

        var hour = DateTime.Now.Hour;
        var timeGreeting = hour switch
        {
            < 12 => "Good Morning",
            < 17 => "Good Afternoon",
            _ => "Good Evening"
        };

        Greeting = IsUserSignedIn && !string.IsNullOrWhiteSpace(UserDisplayName) && UserDisplayName != "Guest"
            ? $"{timeGreeting}, {UserDisplayName}"
            : timeGreeting;
        OnPropertyChanged(nameof(AccountStatusDisplay));
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

            // Sync Account Profile from Feed if present
            if (!string.IsNullOrWhiteSpace(feed.UserName))
            {
                UserDisplayName = feed.UserName;
                IsUserSignedIn = true;
            }
            if (!string.IsNullOrWhiteSpace(feed.UserAvatarUrl))
            {
                UserAvatarUrl = feed.UserAvatarUrl;
            }
            UpdateGreeting();

            // Extract Quick Picks (Exact Android Parity)
            QuickPicks.Clear();
            var quickPickShelf = feed.Shelves.FirstOrDefault(s =>
                s.Title.Contains("Quick pick", StringComparison.OrdinalIgnoreCase) ||
                s.Title.Contains("Start radio", StringComparison.OrdinalIgnoreCase) ||
                s.Title.Contains("Listen again", StringComparison.OrdinalIgnoreCase));

            if (quickPickShelf != null && quickPickShelf.Items.Count > 0)
            {
                foreach (var item in quickPickShelf.Items.Take(8))
                {
                    QuickPicks.Add(item);
                }
            }
            else if (feed.Shelves.Count > 0 && feed.Shelves[0].Items.Count > 0)
            {
                foreach (var item in feed.Shelves[0].Items.Take(8))
                {
                    QuickPicks.Add(item);
                }
            }
            HasQuickPicks = QuickPicks.Count > 0;

            // Update Shelves
            if (feed.Shelves.Count > 0)
            {
                Shelves.Clear();
                foreach (var s in feed.Shelves)
                {
                    if (quickPickShelf != null && s == quickPickShelf && s.Title.Contains("Quick pick", StringComparison.OrdinalIgnoreCase))
                    {
                        // Rendered in dedicated Quick Picks section
                        continue;
                    }
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
                QuickPicks.Clear();

                foreach (var r in results.Take(8))
                {
                    QuickPicks.Add(r);
                }
                HasQuickPicks = QuickPicks.Count > 0;

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

        if (song.VideoId.StartsWith("query:") || song.Duration == TimeSpan.FromMinutes(3.5) || string.IsNullOrWhiteSpace(song.Artist) || song.Artist == "YouTube Music")
        {
            try
            {
                var query = song.VideoId.StartsWith("query:")
                    ? song.VideoId.Substring(6).Replace("|||", " ")
                    : $"{song.Title} {song.Artist}".Replace("YouTube Music", "").Trim();

                var searchResults = await _musicService.SearchAsync(query);
                if (searchResults.Count > 0)
                {
                    var match = searchResults.FirstOrDefault(r => 
                        string.Equals(r.Title, song.Title, StringComparison.OrdinalIgnoreCase))
                        ?? searchResults[0];

                    if (song.VideoId.StartsWith("query:"))
                    {
                        song.VideoId = match.VideoId;
                    }
                    if (match.Duration > TimeSpan.Zero)
                    {
                        song.Duration = match.Duration;
                    }
                    if (!string.IsNullOrWhiteSpace(match.Artist) && match.Artist != "YouTube Music")
                    {
                        song.Artist = match.Artist;
                    }
                    if (!string.IsNullOrWhiteSpace(match.Title))
                    {
                        song.Title = match.Title;
                    }
                    if (string.IsNullOrEmpty(song.ThumbnailUrl))
                    {
                        song.ThumbnailUrl = match.ThumbnailUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to resolve metadata from search", ex);
            }
        }

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
            _playbackService.Enqueue(new Song
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

    public void EnqueueSong(SearchResult? result, bool playNext = false)
    {
        if (result == null) return;
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
        if (playNext)
            _playbackService.EnqueueNext(song);
        else
            _playbackService.Enqueue(song);
    }
}
