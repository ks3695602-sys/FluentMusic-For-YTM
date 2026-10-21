using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using YTMusicClient.ViewModels;

namespace YTMusicClient.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        this.InitializeComponent();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;

        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString() ?? "Default";
            ViewModel.SetThemeCommand.Execute(tag);

            // Apply theme to root element
            if (this.XamlRoot?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ViewModel.CurrentTheme;
            }
        }
    }
}
