using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace YTMusicClient.Converters;

public class ActiveAccentBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush AccentBrush = new(ColorHelper.FromArgb(255, 250, 45, 72)); // #FA2D48
    private static readonly SolidColorBrush SecondaryBrush = new(ColorHelper.FromArgb(180, 160, 160, 170));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b && b)
            return AccentBrush;
        return SecondaryBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
