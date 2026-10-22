using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using YTMusicClient.Services;
using YTMusicClient.ViewModels;

namespace YTMusicClient;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        this.InitializeComponent();

        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IMediaPlaybackService, MediaPlaybackService>();
        services.AddSingleton<IYouTubeMusicService, YouTubeMusicService>();
        services.AddSingleton<ILyricsService, LyricsService>();
        services.AddSingleton<IDownloadService, DownloadService>();

        // Register ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ExploreViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<DownloadsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<PlayerBarViewModel>();
        services.AddSingleton<LyricsViewModel>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
