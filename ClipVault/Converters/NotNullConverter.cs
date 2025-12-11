using System.Globalization;
using Avalonia.Data.Converters;

namespace ClipVault.Converters;

/// <summary>
/// Checks if a value is not null.
/// </summary>
public class NotNullConverter : IValueConverter
{
    public static NotNullConverter Instance { get; } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
