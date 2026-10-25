using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Controls;

public sealed partial class LyricsPanel : UserControl
{
    public LyricsViewModel ViewModel { get; }

    public LyricsPanel()
    {
        ViewModel = App.Services.GetRequiredService<LyricsViewModel>();
        this.InitializeComponent();
        ViewModel.ActiveLyricChanged += OnActiveLyricChanged;
        this.Loaded += (s, e) => UpdatePillState();
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

        ModeBadgeText.Text = ViewModel.LyricsMode == "ENHANCED" ? "Enhanced Karaoke" : "V2 Liquid Glow";
    }

    private async void TransPill_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string mode)
        {
            await ViewModel.SetTranslationModeAsync(mode);
            UpdatePillState();
        }
    }

    private void OnActiveLyricChanged(object? sender, int activeIndex)
    {
        if (activeIndex >= 0 && activeIndex < ViewModel.Lyrics.Count)
        {
            // Smooth scroll to the active lyric line
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
