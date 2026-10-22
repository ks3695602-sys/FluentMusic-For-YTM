using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        this.InitializeComponent();

        ViewModel = App.Services.GetRequiredService<ShellViewModel>();

        // Zero-titlebar edge-to-edge window shell with draggable sidebar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(SidebarDragArea);

        // Make window caption buttons transparent so they seamlessly blend
        ConfigureTransparentCaptionButtons();

        // Configure authentic Windows 11 Mica Alt backdrop
        TrySetBackdrop();

        // Window size & position
        WindowHelper.SetWindowSize(this, 1280, 820);
        WindowHelper.CenterOnScreen(this);

        // Register navigation frame
        var navService = App.Services.GetRequiredService<INavigationService>();
        navService.Frame = ContentFrame;

        // Wire Floating PlayerBar lyrics toggle event
        PlayerBarControl.LyricsToggled += (s, e) =>
        {
            LyricsSplitView.IsPaneOpen = !LyricsSplitView.IsPaneOpen;
        };

        // Navigate to Home by default
        ContentFrame.Navigate(typeof(HomePage));
        SetActiveNavButton(0);
    }

    private void ConfigureTransparentCaptionButtons()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow?.TitleBar != null)
            {
                appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.Color.FromArgb(40, 255, 255, 255);
                appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.Color.FromArgb(60, 255, 255, 255);
            }
        }
        catch { }
    }

    private void TrySetBackdrop()
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            this.SystemBackdrop = new MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            };
        }
        else if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
        {
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }

    private void SetActiveNavButton(int index)
    {
        var activeBg = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        var inactiveBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        NavHomeBtn.Background = index == 0 ? activeBg : inactiveBg;
        NavExploreBtn.Background = index == 1 ? activeBg : inactiveBg;
        NavLibraryBtn.Background = index == 2 ? activeBg : inactiveBg;
        NavSettingsBtn.Background = index == 3 ? activeBg : inactiveBg;
    }

    private void NavHome_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HomePage));
        SetActiveNavButton(0);
    }

    private void NavExplore_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ExplorePage));
        SetActiveNavButton(1);
    }

    private void NavLibrary_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(LibraryPage));
        SetActiveNavButton(2);
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        SetActiveNavButton(3);
    }

    private void UserAccountPill_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        SetActiveNavButton(3);
    }
}
