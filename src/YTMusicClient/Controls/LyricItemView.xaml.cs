using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using YTMusicClient.Models;

namespace YTMusicClient.Controls;

public sealed partial class LyricItemView : UserControl
{
    private static readonly Windows.UI.Text.FontWeight FontWeightBold = new() { Weight = 700 };
    private static readonly Windows.UI.Text.FontWeight FontWeightSemiBold = new() { Weight = 600 };
    private static readonly Windows.UI.Text.FontWeight FontWeightNormal = new() { Weight = 400 };

    private LyricLine? _currentLine;

    public LyricItemView()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        this.Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_currentLine != null)
        {
            _currentLine.PropertyChanged -= OnLinePropertyChanged;
        }

        _currentLine = args.NewValue as LyricLine;

        if (_currentLine != null)
        {
            _currentLine.PropertyChanged += OnLinePropertyChanged;
            UpdateDisplay();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_currentLine != null)
        {
            _currentLine.PropertyChanged -= OnLinePropertyChanged;
            _currentLine = null;
        }
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricLine.IsActive) ||
            e.PropertyName == nameof(LyricLine.ActiveWordsText) ||
            e.PropertyName == nameof(LyricLine.RemainingWordsText) ||
            e.PropertyName == nameof(LyricLine.Text) ||
            e.PropertyName == nameof(LyricLine.TranslatedText))
        {
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (_currentLine == null) return;

        // Load ArchiveTune Lyrics Animation Settings
        string lyricsMode = "V2";
        double bounceFactor = 1.0;
        double glowFactor = 1.0;
        bool lrcBounceEnabled = true;

        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values["LyricsMode"] is string m && !string.IsNullOrWhiteSpace(m)) lyricsMode = m;
            if (localSettings.Values["LyricsV2BounceFactor"] is double bf) bounceFactor = bf;
            if (localSettings.Values["LyricsV2GlowFactor"] is double gf) glowFactor = gf;
            if (localSettings.Values["LyricsV2LrcBounceEnabled"] is bool be) lrcBounceEnabled = be;
        }
        catch { }

        // 1. Inactive State
        if (!_currentLine.IsActive)
        {
            LineTextBlock.FontSize = 15;
            LineTextBlock.Opacity = 0.45;
            LineTextBlock.FontWeight = FontWeightNormal;
            LineTextBlock.Inlines.Clear();
            LineTextBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            LineTextBlock.Text = _currentLine.Text;
            RootStack.Translation = new System.Numerics.Vector3(0, 0, 0);
            RootStack.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
        }
        else // 2. Active State
        {
            LineTextBlock.Opacity = 1.0;

            if (lyricsMode == "V2")
            {
                // ArchiveTune Style 1: V2 Liquid Glow & Spring Bounce
                LineTextBlock.FontSize = 21;
                LineTextBlock.FontWeight = FontWeightBold;

                float bounceScale = 1.0f + (float)(0.06 * bounceFactor);
                if (lrcBounceEnabled)
                {
                    RootStack.Scale = new System.Numerics.Vector3(bounceScale, bounceScale, 1f);
                }

                if (_currentLine.HasWords && !string.IsNullOrEmpty(_currentLine.ActiveWordsText))
                {
                    LineTextBlock.Text = null;
                    LineTextBlock.Inlines.Clear();

                    var activeRun = new Run
                    {
                        Text = _currentLine.ActiveWordsText,
                        Foreground = (Brush)Application.Current.Resources["AccentRedBrush"],
                        FontWeight = FontWeightBold
                    };
                    LineTextBlock.Inlines.Add(activeRun);

                    if (!string.IsNullOrEmpty(_currentLine.RemainingWordsText))
                    {
                        var remRun = new Run
                        {
                            Text = _currentLine.RemainingWordsText,
                            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                            FontWeight = FontWeightSemiBold
                        };
                        LineTextBlock.Inlines.Add(remRun);
                    }
                }
                else
                {
                    LineTextBlock.Inlines.Clear();
                    LineTextBlock.Text = _currentLine.Text;
                    LineTextBlock.Foreground = (Brush)Application.Current.Resources["AccentRedBrush"];
                    LineTextBlock.FontWeight = FontWeightBold;
                }
            }
            else
            {
                // ArchiveTune Style 2: Enhanced Syllable Morphing (Apple Karaoke)
                LineTextBlock.FontSize = 20;
                LineTextBlock.FontWeight = FontWeightSemiBold;
                RootStack.Scale = new System.Numerics.Vector3(1.02f, 1.02f, 1f);

                if (_currentLine.HasWords && !string.IsNullOrEmpty(_currentLine.ActiveWordsText))
                {
                    LineTextBlock.Text = null;
                    LineTextBlock.Inlines.Clear();

                    var activeRun = new Run
                    {
                        Text = _currentLine.ActiveWordsText,
                        Foreground = (Brush)Application.Current.Resources["AccentRedBrush"],
                        FontWeight = FontWeightBold
                    };
                    LineTextBlock.Inlines.Add(activeRun);

                    if (!string.IsNullOrEmpty(_currentLine.RemainingWordsText))
                    {
                        var remRun = new Run
                        {
                            Text = _currentLine.RemainingWordsText,
                            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                            FontWeight = FontWeightNormal
                        };
                        LineTextBlock.Inlines.Add(remRun);
                    }
                }
                else
                {
                    LineTextBlock.Inlines.Clear();
                    LineTextBlock.Text = _currentLine.Text;
                    LineTextBlock.Foreground = (Brush)Application.Current.Resources["AccentRedBrush"];
                    LineTextBlock.FontWeight = FontWeightBold;
                }
            }
        }

        // Translation rendering
        if (!string.IsNullOrWhiteSpace(_currentLine.TranslatedText))
        {
            TranslationTextBlock.Text = _currentLine.TranslatedText;
            TranslationTextBlock.Visibility = Visibility.Visible;
            TranslationTextBlock.Opacity = _currentLine.IsActive ? 0.9 : 0.45;
        }
        else
        {
            TranslationTextBlock.Visibility = Visibility.Collapsed;
        }
    }
}
