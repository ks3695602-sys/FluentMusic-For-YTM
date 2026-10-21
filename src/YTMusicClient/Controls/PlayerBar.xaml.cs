using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Controls;

public sealed partial class PlayerBar : UserControl
{
    public PlayerBarViewModel ViewModel { get; }

    public event EventHandler? LyricsToggled;

    public PlayerBar()
    {
        ViewModel = App.Services.GetRequiredService<PlayerBarViewModel>();
        this.InitializeComponent();
    }

    private void LyricsButton_Click(object sender, RoutedEventArgs e)
    {
        LyricsToggled?.Invoke(this, EventArgs.Empty);
    }
}
