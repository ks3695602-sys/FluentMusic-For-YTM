using Microsoft.UI.Xaml.Controls;

namespace YTMusicClient.Services;

public interface INavigationService
{
    bool CanGoBack { get; }
    Frame? Frame { get; set; }
    void NavigateTo(Type pageType, object? parameter = null);
    void GoBack();
}
