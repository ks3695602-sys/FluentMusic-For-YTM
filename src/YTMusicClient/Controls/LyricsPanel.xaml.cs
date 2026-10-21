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
}
