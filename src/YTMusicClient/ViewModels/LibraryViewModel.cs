using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IMediaPlaybackService _playbackService;

    [ObservableProperty]
    private string _selectedFilter = "All";

    public ObservableCollection<Song> LikedSongs { get; } = new();
    public ObservableCollection<Playlist> Playlists { get; } = new();

    public LibraryViewModel(IMediaPlaybackService playbackService)
    {
        _playbackService = playbackService;
    }

    [RelayCommand]
    private async Task PlaySongAsync(Song? song)
    {
        if (song == null) return;
        await _playbackService.PlayAsync(song);
    }

    [RelayCommand]
    private void AddToLiked()
    {
        var current = _playbackService.CurrentSong;
        if (current != null && !LikedSongs.Any(s => s.VideoId == current.VideoId))
        {
            LikedSongs.Add(current);
        }
    }
}
