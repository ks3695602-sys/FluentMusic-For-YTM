using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class ExploreViewModel : ObservableObject
{
    private readonly IYouTubeMusicService _musicService;
    private readonly IMediaPlaybackService _playbackService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _hasResults;

    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    public ExploreViewModel(IYouTubeMusicService musicService, IMediaPlaybackService playbackService)
    {
        _musicService = musicService;
        _playbackService = playbackService;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || IsSearching) return;
        IsSearching = true;

        try
        {
            SearchResults.Clear();
            var results = await _musicService.SearchAsync(SearchQuery);
            foreach (var r in results)
                SearchResults.Add(r);
            HasResults = SearchResults.Count > 0;
        }
        finally
        {
            IsSearching = false;
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
