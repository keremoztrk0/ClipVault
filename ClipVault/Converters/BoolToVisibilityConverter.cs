using System.Globalization;
using Avalonia.Data.Converters;

namespace ClipVault.Converters;

/// <summary>
/// Converts a boolean to visibility (true = visible, false = collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public static BoolToVisibilityConverter Instance { get; } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // If parameter is "Invert", invert the logic
            if (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                return !boolValue;
            }
            return boolValue;
        }
        
        return false;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            if (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                return !boolValue;
            }
            return boolValue;
        }
        
        return false;
    }
}
