using Microsoft.UI.Xaml.Data;

namespace YTMusicClient.Converters;

public class BoolToGlyphConverter : IValueConverter
{
    public string TrueGlyph { get; set; } = "\uE769"; // Pause
    public string FalseGlyph { get; set; } = "\uE768"; // Play

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? TrueGlyph : FalseGlyph;
        return FalseGlyph;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
