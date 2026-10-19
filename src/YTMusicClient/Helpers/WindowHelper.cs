using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace YTMusicClient.Helpers;

public static class WindowHelper
{
    public static AppWindow GetAppWindow(Window window)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    public static void SetWindowSize(Window window, int width, int height)
    {
        var appWindow = GetAppWindow(window);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    public static void CenterOnScreen(Window window)
    {
        var appWindow = GetAppWindow(window);
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var centeredPosition = appWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
            appWindow.Move(centeredPosition);
        }
    }
}
