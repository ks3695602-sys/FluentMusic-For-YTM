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
            e.PropertyName == nameof(LyricLine.Text))
        {
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (_currentLine == null) return;

        if (!_currentLine.IsActive)
        {
            LineTextBlock.FontSize = 15;
            LineTextBlock.Opacity = 0.5;
            LineTextBlock.FontWeight = FontWeightNormal;
            LineTextBlock.Inlines.Clear();
            LineTextBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            LineTextBlock.Text = _currentLine.Text;
        }
        else
        {
            LineTextBlock.FontSize = 20;
            LineTextBlock.Opacity = 1.0;
            LineTextBlock.FontWeight = FontWeightSemiBold;

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
}
