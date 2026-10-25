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
        ViewModel.ActiveLyricChanged += OnActiveLyricChanged;
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
                        var payload = JsonSerializer.Serialize(new { type = "timeupdate", timeMs });
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
                startSec = l.Timestamp.TotalSeconds
            }).ToList();

            var payload = JsonSerializer.Serialize(new { type = "lyrics", data = lines });
            SpicyLyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
        }
        catch { }
    }

    private void UpdatePillState()
    {
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

        ModeBadgeText.Text = "Spicy Lyrics 🌶️";
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

    private void OnActiveLyricChanged(object? sender, int activeIndex)
    {
        if (activeIndex >= 0 && activeIndex < ViewModel.Lyrics.Count)
        {
            LyricsListView.UpdateLayout();
            var item = LyricsListView.ContainerFromIndex(activeIndex);
            if (item is ListViewItem listViewItem)
            {
                listViewItem.StartBringIntoView(new Microsoft.UI.Xaml.BringIntoViewOptions
                {
                    AnimationDesired = true,
                    VerticalAlignmentRatio = 0.35
                });
            }
        }
    }

    private void LyricsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Models.LyricLine line)
        {
            ViewModel.SeekToLine(line);
        }
    }
}
