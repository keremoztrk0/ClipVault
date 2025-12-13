using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Serilog;

namespace ClipVault.App.Converters;

/// <summary>
/// Converts a file path string to a Bitmap image.
/// Only loads files with recognized image extensions.
/// </summary>
public class FilePathToImageConverter : IValueConverter
{
    private static readonly ILogger Logger = Log.ForContext<FilePathToImageConverter>();
    
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico", ".tiff", ".tif", ".heic", ".heif"
    };
    
    public static FilePathToImageConverter Instance { get; } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string filePath || string.IsNullOrEmpty(filePath))
            return null;

        // Check if the file has an image extension before attempting to load
        string extension = Path.GetExtension(filePath);
        if (!ImageExtensions.Contains(extension))
        {
            return null;
        }

        try
        {
            if (File.Exists(filePath))
            {
                Logger.Debug("Loading image from: {FilePath}", filePath);
                using FileStream stream = File.OpenRead(filePath);
                Bitmap bitmap = new (stream);
                Logger.Debug("Image loaded successfully: {Width}x{Height}", bitmap.PixelSize.Width, bitmap.PixelSize.Height);
                return bitmap;
            }

            Logger.Debug("Image file not found: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load image from: {FilePath}", filePath);
        }
        
        return null;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // One-way conversion only
        return null;
    }
}
