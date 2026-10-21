using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using YTMusicClient.Helpers;
using YTMusicClient.Models;
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

        // Edge-to-edge window shell with draggable sidebar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(SidebarDragArea);

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

        // Configure backdrop: Mica Alt -> Desktop Acrylic fallback
        TrySetBackdrop();

        // Set window size and center
        WindowHelper.SetWindowSize(this, 1280, 820);
        WindowHelper.CenterOnScreen(this);

        // Register the navigation frame
        var navService = App.Services.GetRequiredService<INavigationService>();
        navService.Frame = ContentFrame;

        // Wire PlayerBar drawer toggle events
        PlayerBarControl.LyricsToggled += (s, e) =>
        {
            SwitchDrawerTab(1);
            RightDrawerSplitView.IsPaneOpen = !RightDrawerSplitView.IsPaneOpen;
        };

        PlayerBarControl.QueueToggled += (s, e) =>
        {
            SwitchDrawerTab(0);
            RightDrawerSplitView.IsPaneOpen = !RightDrawerSplitView.IsPaneOpen;
        };

        // Navigate to Home by default
        ContentFrame.Navigate(typeof(HomePage));
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
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }

    private void SwitchDrawerTab(int tabIndex)
    {
        UpNextView.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        LyricsView.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        HistoryView.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

        var activeBg = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        var inactiveBg = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        TabUpNextBtn.Background = tabIndex == 0 ? activeBg : inactiveBg;
        TabLyricsBtn.Background = tabIndex == 1 ? activeBg : inactiveBg;
        TabHistoryBtn.Background = tabIndex == 2 ? activeBg : inactiveBg;
    }

    private void TabUpNextBtn_Click(object sender, RoutedEventArgs e) => SwitchDrawerTab(0);
    private void TabLyricsBtn_Click(object sender, RoutedEventArgs e) => SwitchDrawerTab(1);
    private void TabHistoryBtn_Click(object sender, RoutedEventArgs e) => SwitchDrawerTab(2);

    private void CloseDrawerBtn_Click(object sender, RoutedEventArgs e)
    {
        RightDrawerSplitView.IsPaneOpen = false;
    }

    private void ClearQueue_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PlaybackService.ClearQueue();
    }

    private void RemoveQueueItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Song song)
        {
            ViewModel.PlaybackService.RemoveFromQueue(song);
        }
    }

    private async void QueueItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Song song)
        {
            await ViewModel.PlaybackService.PlayAsync(song);
        }
    }

    private void NavSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void NavHomeBtn_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void NavExploreBtn_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ExplorePage));
    }

    private void NavLibraryBtn_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(LibraryPage));
    }

    private void SettingsShortcut_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
    }

    private void UserProfile_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
    }
}
