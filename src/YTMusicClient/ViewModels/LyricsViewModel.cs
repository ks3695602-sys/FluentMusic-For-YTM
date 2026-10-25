using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YTMusicClient.Models;
using YTMusicClient.Services;

namespace YTMusicClient.ViewModels;

public partial class LyricsViewModel : ObservableObject, IDisposable
{
    private readonly ILyricsService _lyricsService;
    private readonly IMediaPlaybackService _playbackService;

    public IMediaPlaybackService PlaybackService => _playbackService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasLyrics;

    [ObservableProperty]
    private string _statusMessage = "Play a song to see lyrics";

    [ObservableProperty]
    private int _activeLyricIndex = -1;

    [ObservableProperty]
    private string _lyricsMode = "V2"; // "V2" (Liquid Glow & Spring Bounce) or "ENHANCED" (Apple Syllable Morphing)

    [ObservableProperty]
    private double _bounceFactor = 1.0;

    [ObservableProperty]
    private double _glowFactor = 1.0;

    [ObservableProperty]
    private double _fillTransitionWidth = 8.0;

    [ObservableProperty]
    private bool _lrcBounceEnabled = true;

    [ObservableProperty]
    private string _selectedTranslationMode = "Original"; // "Original", "EN", "HI", "Hinglish"

    public ObservableCollection<LyricLine> Lyrics { get; } = new();

    public event EventHandler<int>? ActiveLyricChanged;

    public LyricsViewModel(ILyricsService lyricsService, IMediaPlaybackService playbackService)
    {
        _lyricsService = lyricsService;
        _playbackService = playbackService;
        _playbackService.SongChanged += OnSongChanged;
        _playbackService.PositionChanged += OnPositionChanged;

        LoadPreferences();

        if (_playbackService.CurrentSong != null)
        {
            _ = LoadLyricsAsync(_playbackService.CurrentSong);
        }
    }

    public void LoadPreferences()
    {
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values["LyricsMode"] is string mode && !string.IsNullOrWhiteSpace(mode))
                LyricsMode = mode;
            if (localSettings.Values["LyricsV2BounceFactor"] is double bounce)
                BounceFactor = bounce;
            if (localSettings.Values["LyricsV2GlowFactor"] is double glow)
                GlowFactor = glow;
            if (localSettings.Values["LyricsV2FillTransitionWidth"] is double fill)
                FillTransitionWidth = fill;
            if (localSettings.Values["LyricsV2LrcBounceEnabled"] is bool lrcBounce)
                LrcBounceEnabled = lrcBounce;
            if (localSettings.Values["LyricsTranslationMode"] is string transMode && !string.IsNullOrWhiteSpace(transMode))
                SelectedTranslationMode = transMode;
        }
        catch { }
    }

    [RelayCommand]
    public async Task SetTranslationModeAsync(string mode)
    {
        SelectedTranslationMode = mode;
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values["LyricsTranslationMode"] = mode;
        }
        catch { }

        if (mode == "Original" || string.IsNullOrWhiteSpace(mode))
        {
            foreach (var line in Lyrics)
            {
                line.TranslatedText = null;
            }
            return;
        }

        string targetLang = mode switch
        {
            "EN" => "en",
            "HI" => "hi",
            "Hinglish" => "hinglish",
            _ => "en"
        };

        await TranslateCurrentLyricsAsync(targetLang);
    }

    private async Task TranslateCurrentLyricsAsync(string targetLang)
    {
        if (Lyrics.Count == 0) return;

        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        var snapshot = Lyrics.ToList();

        _ = Task.Run(async () =>
        {
            foreach (var line in snapshot)
            {
                if (string.IsNullOrWhiteSpace(line.Text)) continue;
                try
                {
                    var trans = await _lyricsService.TranslateAsync(line.Text, targetLang);
                    dispatcherQueue?.TryEnqueue(() =>
                    {
                        line.TranslatedText = trans;
                    });
                }
                catch { }
            }
        });
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
                song.Title, song.Artist, song.Duration, song.VideoId);

            if (lines.Count > 0)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];

                    // If line has no word-level timestamps (e.g. standard LRCLIB), auto-generate them
                    if (line.Words.Count == 0 && !string.IsNullOrWhiteSpace(line.Text))
                    {
                        var rawWords = line.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (rawWords.Length > 0)
                        {
                            TimeSpan lineDuration;
                            if (line.EndTime > line.Timestamp)
                            {
                                lineDuration = line.EndTime - line.Timestamp;
                            }
                            else if (i + 1 < lines.Count && lines[i + 1].Timestamp > line.Timestamp)
                            {
                                lineDuration = lines[i + 1].Timestamp - line.Timestamp;
                                if (lineDuration.TotalSeconds > 8) lineDuration = TimeSpan.FromSeconds(8);
                            }
                            else
                            {
                                lineDuration = TimeSpan.FromSeconds(Math.Max(2.5, rawWords.Length * 0.45));
                            }

                            var wordDurationMs = lineDuration.TotalMilliseconds / rawWords.Length;
                            for (int w = 0; w < rawWords.Length; w++)
                            {
                                var wStart = line.Timestamp + TimeSpan.FromMilliseconds(w * wordDurationMs);
                                var wEnd = wStart + TimeSpan.FromMilliseconds(wordDurationMs);
                                line.Words.Add(new LyricWord
                                {
                                    Text = rawWords[w],
                                    StartTime = wStart,
                                    EndTime = wEnd
                                });
                            }
                        }
                    }

                    line.RemainingWordsText = line.Text;
                    line.ActiveWordsText = string.Empty;
                    Lyrics.Add(line);
                }

                HasLyrics = true;
                StatusMessage = string.Empty;

                if (SelectedTranslationMode != "Original" && !string.IsNullOrWhiteSpace(SelectedTranslationMode))
                {
                    string targetLang = SelectedTranslationMode switch
                    {
                        "EN" => "en",
                        "HI" => "hi",
                        "Hinglish" => "hinglish",
                        _ => "en"
                    };
                    await TranslateCurrentLyricsAsync(targetLang);
                }
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
            // Deactivate previous line
            if (ActiveLyricIndex >= 0 && ActiveLyricIndex < Lyrics.Count)
            {
                var prevLine = Lyrics[ActiveLyricIndex];
                prevLine.IsActive = false;
                prevLine.ActiveWordsText = string.Empty;
                prevLine.RemainingWordsText = prevLine.Text;
                foreach (var w in prevLine.Words)
                {
                    w.IsActive = false;
                }
            }

            // Activate current line
            if (newActiveIndex >= 0 && newActiveIndex < Lyrics.Count)
            {
                Lyrics[newActiveIndex].IsActive = true;
            }

            ActiveLyricIndex = newActiveIndex;
            ActiveLyricChanged?.Invoke(this, newActiveIndex);
        }

        // Real-time word-by-word karaoke synchronization for active line
        if (ActiveLyricIndex >= 0 && ActiveLyricIndex < Lyrics.Count)
        {
            var activeLine = Lyrics[ActiveLyricIndex];
            if (activeLine.Words.Count > 0)
            {
                int activeCount = 0;
                for (int w = 0; w < activeLine.Words.Count; w++)
                {
                    bool isActive = position >= activeLine.Words[w].StartTime;
                    activeLine.Words[w].IsActive = isActive;
                    if (isActive) activeCount++;
                }

                if (activeCount == 0)
                {
                    activeLine.ActiveWordsText = string.Empty;
                    activeLine.RemainingWordsText = activeLine.Text;
                }
                else if (activeCount >= activeLine.Words.Count)
                {
                    activeLine.ActiveWordsText = activeLine.Text;
                    activeLine.RemainingWordsText = string.Empty;
                }
                else
                {
                    activeLine.ActiveWordsText = string.Join(" ", activeLine.Words.Take(activeCount).Select(w => w.Text));
                    activeLine.RemainingWordsText = " " + string.Join(" ", activeLine.Words.Skip(activeCount).Select(w => w.Text));
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
