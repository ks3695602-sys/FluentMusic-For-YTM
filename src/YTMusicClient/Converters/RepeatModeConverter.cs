using System;
using Microsoft.UI.Xaml.Data;

namespace YTMusicClient.Converters;

public class RepeatModeToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int mode)
        {
            return mode switch
            {
                2 => "\uE8ED", // Repeat One
                1 => "\uE8EE", // Repeat All
                _ => "\uE8EE"  // Normal Repeat icon
            };
        }
        return "\uE8EE";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
