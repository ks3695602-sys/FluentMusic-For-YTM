using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Controls;

public sealed partial class LyricsPanel : UserControl
{
    public LyricsViewModel ViewModel { get; }
    private bool _isWebViewInitialized = false;

    public LyricsPanel()
    {
        ViewModel = App.Services.GetRequiredService<LyricsViewModel>();
        this.InitializeComponent();
        this.Loaded += (s, e) =>
        {
            InitializeSpicyLyricsAsync();
        };

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.Lyrics) || e.PropertyName == nameof(ViewModel.HasLyrics))
            {
                PushLyricsToSpicy();
            }
            else if (e.PropertyName == nameof(ViewModel.StatusMessage) || e.PropertyName == nameof(ViewModel.IsLoading))
            {
                PushStatusToSpicy();
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedTranslationMode))
            {
                PushTranslationModeToSpicy();
            }
        };

        ViewModel.PlaybackService.PositionChanged += (s, pos) =>
        {
            if (_isWebViewInitialized)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var timeMs = pos.TotalMilliseconds;
                        var isPlaying = ViewModel.PlaybackService.IsPlaying;
                        var payload = JsonSerializer.Serialize(new { type = "timeupdate", timeMs, isPlaying });
                        SpicyLyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
                    }
                    catch { }
                });
            }
        };

        ViewModel.PlaybackService.PlaybackStateChanged += (s, state) =>
        {
            if (_isWebViewInitialized)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var timeMs = ViewModel.PlaybackService.Position.TotalMilliseconds;
                        var isPlaying = ViewModel.PlaybackService.IsPlaying;
                        var payload = JsonSerializer.Serialize(new { type = "timeupdate", timeMs, isPlaying });
                        SpicyLyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
                    }
                    catch { }
                });
            }
        };
    }

    private async void InitializeSpicyLyricsAsync()
    {
        if (_isWebViewInitialized) return;
        try
        {
            await SpicyLyricsWebView.EnsureCoreWebView2Async();
            SpicyLyricsWebView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            SpicyLyricsWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            SpicyLyricsWebView.CoreWebView2.WebMessageReceived += async (s, e) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                    if (doc.RootElement.TryGetProperty("type", out var typeProp))
                    {
                        var type = typeProp.GetString();
                        if (type == "seek" && doc.RootElement.TryGetProperty("timeMs", out var timeMs))
                        {
                            var sec = timeMs.GetDouble() / 1000.0;
                            ViewModel.PlaybackService.Seek(TimeSpan.FromSeconds(sec));
                        }
                        else if (type == "translation" && doc.RootElement.TryGetProperty("mode", out var modeProp))
                        {
                            var mode = modeProp.GetString() ?? "Original";
                            await ViewModel.SetTranslationModeAsync(mode);
                            PushLyricsToSpicy();
                        }
                    }
                }
                catch { }
            };

            var packagePath = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
            var htmlPath = System.IO.Path.Combine(packagePath, "Assets", "SpicyLyrics", "index.html");
            if (System.IO.File.Exists(htmlPath))
            {
                SpicyLyricsWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            }
            _isWebViewInitialized = true;
            PushLyricsToSpicy();
            PushTranslationModeToSpicy();
        }
        catch { }
    }

    private void PushTranslationModeToSpicy()
    {
        if (!_isWebViewInitialized) return;
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                type = "translationMode",
                mode = ViewModel.SelectedTranslationMode ?? "Original"
            });
            SpicyLyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
        }
        catch { }
    }

    private void PushLyricsToSpicy()
    {
        if (!_isWebViewInitialized) return;
        try
        {
            var lines = ViewModel.Lyrics.Select(l => new
            {
                text = l.Text,
                translation = l.TranslatedText,
                startTimeMs = l.Timestamp.TotalMilliseconds,
                startSec = l.Timestamp.TotalSeconds,
                endTimeMs = l.EndTime.TotalMilliseconds,
                words = l.Words.Select(w => new
                {
                    text = w.Text,
                    startMs = w.StartTime.TotalMilliseconds,
                    endMs = w.EndTime.TotalMilliseconds
                }).ToList()
            }).ToList();

            var currentSong = ViewModel.PlaybackService.CurrentSong;
            var payload = JsonSerializer.Serialize(new
            {
                type = "lyrics",
                title = currentSong?.Title ?? "",
                artist = currentSong?.Artist ?? "",
                album = currentSong?.Album ?? "",
                coverUrl = currentSong?.ThumbnailUrl ?? "",
                data = lines
            });
            SpicyLyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);

            if (currentSong != null && !string.IsNullOrEmpty(currentSong.GradientColorStart))
            {
                var colorPayload = JsonSerializer.Serialize(new
                {
                    type = "colors",
                    colors = new[] { currentSong.GradientColorStart, currentSong.GradientColorEnd }
                });
                SpicyLyricsWebView.CoreWebView2?.PostWebMessageAsJson(colorPayload);
            }

            if (lines.Count == 0)
            {
                PushStatusToSpicy();
            }
        }
        catch { }
    }

    private void PushStatusToSpicy()
    {
        if (!_isWebViewInitialized) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var text = ViewModel.IsLoading
                    ? "Synchronizing lyrics..."
                    : (!string.IsNullOrEmpty(ViewModel.StatusMessage) ? ViewModel.StatusMessage : "No synced lyrics available");

                var payload = JsonSerializer.Serialize(new
                {
                    type = "status",
                    text = text,
                    isLoading = ViewModel.IsLoading
                });
                SpicyLyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
            }
            catch { }
        });
    }
}
