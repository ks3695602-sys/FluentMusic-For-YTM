using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using YTMusicClient.Helpers;
using YTMusicClient.Services;
using YTMusicClient.ViewModels;
using YTMusicClient.Views;

namespace YTMusicClient;

public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        this.InitializeComponent();

        // Custom TitleBar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Set App Window Icon
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch { }

        // Configure backdrop: Mica Alt -> Mica -> Acrylic -> fallback
        TrySetBackdrop();

        // Set window size and center
        WindowHelper.SetWindowSize(this, 1280, 820);
        WindowHelper.CenterOnScreen(this);

        // Register the navigation frame
        var navService = App.Services.GetRequiredService<INavigationService>();
        navService.Frame = ContentFrame;
    }

    private void TrySetBackdrop()
    {
        bool isWindows11 = Environment.OSVersion.Version.Build >= 22000;

        if (isWindows11 && Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            this.SystemBackdrop = new MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            };
        }
        else if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            // Windows 10 native frosted acrylic backdrop support
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }

    private void TopSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = null;
        ContentFrame.Navigate(typeof(SettingsPage));
    }

    private void UserProfileButton_Click(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = null;
        ContentFrame.Navigate(typeof(SettingsPage));
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Navigate to Home by default
        ContentFrame.Navigate(typeof(HomePage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            var pageType = tag switch
            {
                "home" => typeof(HomePage),
                "explore" => typeof(ExplorePage),
                "library" => typeof(LibraryPage),
                "settings" => typeof(SettingsPage),
                _ => typeof(HomePage)
            };
            ContentFrame.Navigate(pageType);
        }
    }
}
