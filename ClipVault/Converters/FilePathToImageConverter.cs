using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Serilog;

namespace ClipVault.Converters;

/// <summary>
/// Converts a file path string to a Bitmap image.
/// </summary>
public class FilePathToImageConverter : IValueConverter
{
    private static readonly ILogger Logger = Log.ForContext<FilePathToImageConverter>();
    
    public static FilePathToImageConverter Instance { get; } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string filePath && !string.IsNullOrEmpty(filePath))
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Logger.Debug("Loading image from: {FilePath}", filePath);
                    using var stream = File.OpenRead(filePath);
                    var bitmap = new Bitmap(stream);
                    Logger.Debug("Image loaded successfully: {Width}x{Height}", bitmap.PixelSize.Width, bitmap.PixelSize.Height);
                    return bitmap;
                }
                else
                {
                    Logger.Warning("Image file not found: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load image from: {FilePath}", filePath);
            }
        }
        
        return null;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // One-way conversion only
        return null;
    }
}
