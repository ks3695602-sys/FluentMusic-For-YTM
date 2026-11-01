using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using YTMusicClient.Models;
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
            InitializeFlutterLyricsAsync();
        };

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.Lyrics) || e.PropertyName == nameof(ViewModel.HasLyrics))
            {
                PushLyricsToFlutter();
            }
            else if (e.PropertyName == nameof(ViewModel.LyricsOffsetMs))
            {
                PushOffsetToFlutter();
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
                        var effectiveTimeMs = pos.TotalMilliseconds + ViewModel.LyricsOffsetMs;
                        var isPlaying = ViewModel.PlaybackService.IsPlaying;
                        var payload = JsonSerializer.Serialize(new { type = "timeupdate", timeMs = effectiveTimeMs, isPlaying });
                        LyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
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
                        var effectiveTimeMs = ViewModel.PlaybackService.Position.TotalMilliseconds + ViewModel.LyricsOffsetMs;
                        var isPlaying = ViewModel.PlaybackService.IsPlaying;
                        var payload = JsonSerializer.Serialize(new { type = "timeupdate", timeMs = effectiveTimeMs, isPlaying });
                        LyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
                    }
                    catch { }
                });
            }
        };
    }

    private async void InitializeFlutterLyricsAsync()
    {
        if (_isWebViewInitialized) return;
        try
        {
            await LyricsWebView.EnsureCoreWebView2Async();
            LyricsWebView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            LyricsWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            LyricsWebView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                    if (doc.RootElement.TryGetProperty("type", out var typeProp))
                    {
                        var type = typeProp.GetString();
                        if (type == "seek" && doc.RootElement.TryGetProperty("timeMs", out var timeMsProp))
                        {
                            var sec = timeMsProp.GetDouble() / 1000.0;
                            ViewModel.PlaybackService.Seek(TimeSpan.FromSeconds(Math.Max(0, sec)));
                        }
                    }
                }
                catch { }
            };

            var packagePath = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
            var htmlPath = Path.Combine(packagePath, "Assets", "FlutterLyric", "index.html");
            if (File.Exists(htmlPath))
            {
                LyricsWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            }
            _isWebViewInitialized = true;

            PushOffsetToFlutter();
            PushLyricsToFlutter();
        }
        catch { }
    }

    private void PushOffsetToFlutter()
    {
        if (!_isWebViewInitialized) return;
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                type = "offset",
                offsetMs = ViewModel.LyricsOffsetMs
            });
            LyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
        }
        catch { }
    }

    private void PushLyricsToFlutter()
    {
        if (!_isWebViewInitialized) return;
        try
        {
            var qrc = GenerateQrc(ViewModel.Lyrics);
            var trans = GenerateTransLrc(ViewModel.Lyrics);
            var currentSong = ViewModel.PlaybackService.CurrentSong;

            var payload = JsonSerializer.Serialize(new
            {
                type = "lyrics",
                qrc = qrc,
                trans = trans,
                title = currentSong?.Title ?? "",
                artist = currentSong?.Artist ?? ""
            });
            LyricsWebView.CoreWebView2?.PostWebMessageAsJson(payload);
        }
        catch { }
    }

    private static string GenerateQrc(IEnumerable<LyricLine> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            long lineStartMs = (long)line.Timestamp.TotalMilliseconds;
            long lineDurationMs = (long)(line.EndTime - line.Timestamp).TotalMilliseconds;
            if (lineDurationMs <= 0) lineDurationMs = 3000;

            if (line.HasWords)
            {
                sb.Append($"[{lineStartMs},{lineDurationMs}]");
                foreach (var word in line.Words)
                {
                    long wordStartMs = (long)word.StartTime.TotalMilliseconds;
                    long wordDurMs = (long)(word.EndTime - word.StartTime).TotalMilliseconds;
                    if (wordDurMs <= 0) wordDurMs = 250;
                    sb.Append($"{word.Text}({wordStartMs},{wordDurMs})");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"[{lineStartMs},{lineDurationMs}]{line.Text}({lineStartMs},{lineDurationMs})");
            }
        }
        return sb.ToString();
    }

    private static string GenerateTransLrc(IEnumerable<LyricLine> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line.TranslatedText))
            {
                var ts = line.Timestamp;
                sb.AppendLine($"[{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}]{line.TranslatedText}");
            }
        }
        return sb.ToString();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Resources.TryGetValue("LyricsSettingsFlyout", out var obj) && obj is Flyout flyout && sender is FrameworkElement element)
        {
            flyout.ShowAt(element);
        }
    }

    private void OffsetSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        ViewModel.SetOffset(e.NewValue);
        PushOffsetToFlutter();
    }

    private void NudgeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && double.TryParse(tagStr, out var delta))
        {
            var newOffset = ViewModel.LyricsOffsetMs + delta;
            ViewModel.SetOffset(newOffset);
            PushOffsetToFlutter();
        }
    }

    private void ResetOffset_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetOffset(0);
        PushOffsetToFlutter();
    }

    private void Preset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string preset)
        {
            ViewModel.SetPreset(preset);
        }
    }

    private void FontScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        ViewModel.SetFontScale(e.NewValue);
    }

    private void WordByWord_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch ts)
        {
            ViewModel.SetWordByWord(ts.IsOn);
        }
    }

    private void Translations_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch ts)
        {
            ViewModel.ShowTranslations = ts.IsOn;
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values["LyricsShowTranslations"] = ts.IsOn;
            }
            catch { }
        }
    }
}
