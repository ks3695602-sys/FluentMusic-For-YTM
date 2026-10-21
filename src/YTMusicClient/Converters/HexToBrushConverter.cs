using System;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace YTMusicClient.Converters;

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                hex = hex.Trim().TrimStart('#');
                byte a = 255;
                int offset = 0;

                if (hex.Length == 8)
                {
                    a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    offset = 2;
                }

                if (hex.Length >= 6)
                {
                    byte r = byte.Parse(hex.Substring(offset, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(hex.Substring(offset + 2, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(hex.Substring(offset + 4, 2), NumberStyles.HexNumber);

                    return new SolidColorBrush(ColorHelper.FromArgb(a, r, g, b));
                }
            }
            catch { }
        }

        return new SolidColorBrush(ColorHelper.FromArgb(255, 34, 37, 46));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
