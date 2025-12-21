namespace ClipVault.App.Helpers;

/// <summary>
/// Helper class for file type detection and MIME type resolution.
/// </summary>
public static class FileTypeHelper
{
    /// <summary>
    /// Image file extensions.
    /// </summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tiff", ".tif"
    };
    
    /// <summary>
    /// Video file extensions.
    /// </summary>
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm", ".flv", ".m4v", ".mpeg", ".mpg"
    };
    
    /// <summary>
    /// Audio file extensions.
    /// </summary>
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus"
    };
    
    /// <summary>
    /// Document file extensions.
    /// </summary>
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".ods", ".odp"
    };
    
    /// <summary>
    /// Archive file extensions.
    /// </summary>
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz"
    };
    
    /// <summary>
    /// Code/source file extensions.
    /// </summary>
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".go", ".rs", ".rb", ".php",
        ".html", ".css", ".json", ".xml", ".yaml", ".yml", ".md", ".sql"
    };
    
    /// <summary>
    /// Checks if the file extension is an image.
    /// </summary>
    private static bool IsImage(string extension) => ImageExtensions.Contains(extension);
    
    /// <summary>
    /// Checks if the file extension is a video.
    /// </summary>
    private static bool IsVideo(string extension) => VideoExtensions.Contains(extension);
    
    /// <summary>
    /// Checks if the file extension is audio.
    /// </summary>
    private static bool IsAudio(string extension) => AudioExtensions.Contains(extension);
    
    /// <summary>
    /// Checks if the file extension is a document.
    /// </summary>
    private static bool IsDocument(string extension) => DocumentExtensions.Contains(extension);
    
    /// <summary>
    /// Checks if the file extension is an archive.
    /// </summary>
    private static bool IsArchive(string extension) => ArchiveExtensions.Contains(extension);
    
    /// <summary>
    /// Checks if the file extension is a code/source file.
    /// </summary>
    private static bool IsCode(string extension) => CodeExtensions.Contains(extension);
    
    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    public static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            // Images
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".tiff" or ".tif" => "image/tiff",
            
            // Videos
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".webm" => "video/webm",
            ".flv" => "video/x-flv",
            ".m4v" => "video/x-m4v",
            ".mpeg" or ".mpg" => "video/mpeg",
            
            // Audio
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".wma" => "audio/x-ms-wma",
            ".opus" => "audio/opus",
            
            // Documents
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".rtf" => "text/rtf",
            ".odt" => "application/vnd.oasis.opendocument.text",
            ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
            ".odp" => "application/vnd.oasis.opendocument.presentation",
            
            // Code/Web
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".ts" => "application/typescript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".yaml" or ".yml" => "application/x-yaml",
            ".md" => "text/markdown",
            ".sql" => "application/sql",
            
            // Programming languages
            ".cs" => "text/x-csharp",
            ".py" => "text/x-python",
            ".java" => "text/x-java",
            ".cpp" or ".c" or ".h" => "text/x-c",
            ".go" => "text/x-go",
            ".rs" => "text/x-rust",
            ".rb" => "text/x-ruby",
            ".php" => "text/x-php",
            
            // Archives
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".bz2" => "application/x-bzip2",
            ".xz" => "application/x-xz",
            
            _ => "application/octet-stream"
        };
    }
    
    /// <summary>
    /// Gets a human-readable file type description from an extension.
    /// </summary>
    public static string GetFileTypeDescription(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return "Unknown";
        
        if (IsImage(extension)) return "Image";
        if (IsVideo(extension)) return "Video";
        if (IsAudio(extension)) return "Audio";
        if (IsDocument(extension)) return "Document";
        if (IsArchive(extension)) return "Archive";
        if (IsCode(extension)) return "Source Code";
        
        return extension.TrimStart('.').ToUpperInvariant() + " File";
    }
    
    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return order == 0 
            ? $"{size:0} {sizes[order]}" 
            : $"{size:0.##} {sizes[order]}";
    }
}
