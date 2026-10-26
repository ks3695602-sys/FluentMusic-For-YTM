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
            UpdatePillState();
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

            SpicyLyricsWebView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                    if (doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "seek" &&
                        doc.RootElement.TryGetProperty("timeMs", out var timeMs))
                    {
                        var sec = timeMs.GetDouble() / 1000.0;
                        ViewModel.PlaybackService.Seek(TimeSpan.FromSeconds(sec));
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
        }
        catch { }
    }

    private string _currentEngine = "spicy";

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

    private void EngineTab_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string engine)
        {
            _currentEngine = engine;
            UpdateEnginePills();
            if (_isWebViewInitialized)
            {
                var payload = JsonSerializer.Serialize(new { type = "engine", engine = _currentEngine });
                SpicyLyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
            }
        }
    }

    private void UpdateEnginePills()
    {
        var activeBg = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        var transBg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

        EngineSpicyBtn.Background = _currentEngine == "spicy" ? activeBg : transBg;
        EngineSpicyBtn.FontWeight = _currentEngine == "spicy" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

        EngineYouLyBtn.Background = _currentEngine == "youly" ? activeBg : transBg;
        EngineYouLyBtn.FontWeight = _currentEngine == "youly" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
    }

    private void UpdatePillState()
    {
        UpdateEnginePills();
        var mode = ViewModel.SelectedTranslationMode ?? "Original";
        var activeBg = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        var transBg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

        TransOriginalBtn.Background = mode == "Original" ? activeBg : transBg;
        TransOriginalBtn.FontWeight = mode == "Original" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

        TransEnBtn.Background = mode == "EN" ? activeBg : transBg;
        TransEnBtn.FontWeight = mode == "EN" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

        TransHiBtn.Background = mode == "HI" ? activeBg : transBg;
        TransHiBtn.FontWeight = mode == "HI" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Medium;

        TransHinglishBtn.Background = mode == "Hinglish" ? activeBg : transBg;
        TransHinglishBtn.FontWeight = mode == "Hinglish" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
    }

    private async void TransPill_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string mode)
        {
            await ViewModel.SetTranslationModeAsync(mode);
            UpdatePillState();
            PushLyricsToSpicy();
        }
    }
}
