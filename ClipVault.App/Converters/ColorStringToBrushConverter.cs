using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ClipVault.App.Converters;

/// <summary>
/// Converts a hex color string to a Brush.
/// </summary>
public class ColorStringToBrushConverter : IValueConverter
{
    public static ColorStringToBrushConverter Instance { get; } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorString && !string.IsNullOrEmpty(colorString))
        {
            try
            {
                return new SolidColorBrush(Color.Parse(colorString));
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }
        
        return new SolidColorBrush(Colors.Gray);
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color.ToString();
        }
        
        return "#808080";
    }
}
