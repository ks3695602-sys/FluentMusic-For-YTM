using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Controls;

public sealed partial class PlayerBar : UserControl
{
    public PlayerBarViewModel ViewModel { get; }

    public PlayerBar()
    {
        ViewModel = App.Services.GetRequiredService<PlayerBarViewModel>();
        this.InitializeComponent();
    }
}
