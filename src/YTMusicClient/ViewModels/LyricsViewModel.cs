using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class LyricsViewModel : ObservableObject, IDisposable
{
    private readonly ILyricsService _lyricsService;
    private readonly IMediaPlaybackService _playbackService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasLyrics;

    [ObservableProperty]
    private string _statusMessage = "Play a song to see lyrics";

    [ObservableProperty]
    private int _activeLyricIndex = -1;

    public ObservableCollection<LyricLine> Lyrics { get; } = new();

    public event EventHandler<int>? ActiveLyricChanged;

    public LyricsViewModel(ILyricsService lyricsService, IMediaPlaybackService playbackService)
    {
        _lyricsService = lyricsService;
        _playbackService = playbackService;
        _playbackService.SongChanged += OnSongChanged;
        _playbackService.PositionChanged += OnPositionChanged;

        if (_playbackService.CurrentSong != null)
        {
            _ = LoadLyricsAsync(_playbackService.CurrentSong);
        }
    }

    private async void OnSongChanged(object? sender, Song song)
    {
        await LoadLyricsAsync(song);
    }

    [RelayCommand]
    public async Task LoadLyricsAsync(Song? song)
    {
        if (song == null) return;

        IsLoading = true;
        HasLyrics = false;
        StatusMessage = "Searching for lyrics...";
        Lyrics.Clear();
        ActiveLyricIndex = -1;

        try
        {
            var lines = await _lyricsService.GetSyncedLyricsAsync(
                song.Title, song.Artist, song.Duration);

            if (lines.Count > 0)
            {
                foreach (var line in lines)
                    Lyrics.Add(line);
                HasLyrics = true;
                StatusMessage = string.Empty;
            }
            else
            {
                StatusMessage = "No synced lyrics available for this song";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load lyrics: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void SeekToLine(LyricLine? line)
    {
        if (line != null)
        {
            _playbackService.Seek(line.Timestamp);
        }
    }

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        if (!HasLyrics || Lyrics.Count == 0) return;

        int newActiveIndex = -1;

        for (int i = Lyrics.Count - 1; i >= 0; i--)
        {
            if (position >= Lyrics[i].Timestamp)
            {
                newActiveIndex = i;
                break;
            }
        }

        if (newActiveIndex != ActiveLyricIndex)
        {
            // Deactivate previous
            if (ActiveLyricIndex >= 0 && ActiveLyricIndex < Lyrics.Count)
            {
                Lyrics[ActiveLyricIndex].IsActive = false;
                foreach (var w in Lyrics[ActiveLyricIndex].Words)
                {
                    w.IsActive = false;
                }
            }

            // Activate current
            if (newActiveIndex >= 0 && newActiveIndex < Lyrics.Count)
            {
                Lyrics[newActiveIndex].IsActive = true;
            }

            ActiveLyricIndex = newActiveIndex;
            ActiveLyricChanged?.Invoke(this, newActiveIndex);
        }

        // Real-time word-level synchronization for active line
        if (ActiveLyricIndex >= 0 && ActiveLyricIndex < Lyrics.Count)
        {
            var activeLine = Lyrics[ActiveLyricIndex];
            if (activeLine.HasWords)
            {
                foreach (var w in activeLine.Words)
                {
                    w.IsActive = position >= w.StartTime;
                }
            }
        }
    }

    public void Dispose()
    {
        _playbackService.SongChanged -= OnSongChanged;
        _playbackService.PositionChanged -= OnPositionChanged;
    }
}
