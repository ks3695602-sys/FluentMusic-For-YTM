using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace YTMusicClient.Services;

public class NavigationService : INavigationService
{
    private Frame? _frame;

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public Frame? Frame
    {
        get => _frame;
        set
        {
            if (_frame != null)
                _frame.Navigated -= OnNavigated;

            _frame = value;

            if (_frame != null)
                _frame.Navigated += OnNavigated;
        }
    }

    public void NavigateTo(Type pageType, object? parameter = null)
    {
        if (_frame != null && _frame.CurrentSourcePageType != pageType)
        {
            _frame.Navigate(pageType, parameter);
        }
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
            _frame.GoBack();
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        // Could raise events for navigation tracking
    }
}
