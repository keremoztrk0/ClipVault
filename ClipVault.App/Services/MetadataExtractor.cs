using ClipVault.App.Helpers;
using ClipVault.App.Models;
using ClipVault.App.Services.Clipboard;
using Microsoft.Extensions.Logging;

namespace ClipVault.App.Services;

/// <summary>
/// Extracts metadata from clipboard content based on content type.
/// </summary>
public class MetadataExtractor(ILogger<MetadataExtractor> logger) : IMetadataExtractor
{
    public async Task<ClipboardMetadata> ExtractMetadataAsync(ClipboardContent content, Guid clipboardItemId)
    {
        ClipboardMetadata metadata = new ClipboardMetadata
        {
            ClipboardItemId = clipboardItemId
        };
        
        switch (content.Type)
        {
            case ClipboardContentType.Text:
            case ClipboardContentType.RichText:
            case ClipboardContentType.Html:
                ExtractTextMetadata(content, metadata);
                break;
                
            case ClipboardContentType.Image:
                await ExtractImageMetadataAsync(content, metadata);
                break;
                
            case ClipboardContentType.File:
                await ExtractFileMetadataAsync(content.FilePaths?.FirstOrDefault(), metadata);
                break;
                
            case ClipboardContentType.Files:
                ExtractMultipleFilesMetadata(content.FilePaths, metadata);
                break;
                
            case ClipboardContentType.Video:
                await ExtractVideoMetadataAsync(content.FilePaths?.FirstOrDefault(), metadata);
                break;
                
            case ClipboardContentType.Audio:
                await ExtractAudioMetadataAsync(content.FilePaths?.FirstOrDefault(), metadata);
                break;
        }
        
        return metadata;
    }
    
    private static void ExtractTextMetadata(ClipboardContent content, ClipboardMetadata metadata)
    {
        if (string.IsNullOrEmpty(content.Text)) return;
        
        string? text = content.Text;
        
        metadata.CharacterCount = text.Length;
        metadata.WordCount = CountWords(text);
        metadata.LineCount = text.Split('\n').Length;
        metadata.MimeType = content.Type switch
        {
            ClipboardContentType.Html => "text/html",
            ClipboardContentType.RichText => "text/rtf",
            _ => "text/plain"
        };
    }
    
    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        
        int wordCount = 0;
        bool inWord = false;
        
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    wordCount++;
                    inWord = false;
                }
            }
            else
            {
                inWord = true;
            }
        }
        
        if (inWord) wordCount++;
        
        return wordCount;
    }
    
    private async Task ExtractImageMetadataAsync(ClipboardContent content, ClipboardMetadata metadata)
    {
        metadata.MimeType = "image/unknown";
        
        // If we have image data, try to get dimensions
        if (content.ImageData != null && content.ImageData.Length > 0)
        {
            metadata.FileSize = content.ImageData.Length;
            
            // Try to detect image dimensions from header bytes
            (int Width, int Height)? dimensions = TryGetImageDimensions(content.ImageData);
            if (dimensions.HasValue)
            {
                metadata.Width = dimensions.Value.Width;
                metadata.Height = dimensions.Value.Height;
            }
        }
        
        // If we have a file path, get more info
        if (content.FilePaths?.FirstOrDefault() is { } filePath)
        {
            await ExtractFileMetadataAsync(filePath, metadata);
        }
        
        await Task.CompletedTask;
    }
    
    private static (int Width, int Height)? TryGetImageDimensions(byte[] imageData)
    {
        if (imageData.Length < 24) return null;
        
        // PNG
        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
        {
            int width = (imageData[16] << 24) | (imageData[17] << 16) | (imageData[18] << 8) | imageData[19];
            int height = (imageData[20] << 24) | (imageData[21] << 16) | (imageData[22] << 8) | imageData[23];
            return (width, height);
        }
        
        // JPEG
        if (imageData[0] == 0xFF && imageData[1] == 0xD8)
        {
            // JPEG dimension extraction is complex, skip for now
            return null;
        }
        
        // BMP
        if (imageData[0] == 0x42 && imageData[1] == 0x4D && imageData.Length >= 26)
        {
            int width = BitConverter.ToInt32(imageData, 18);
            int height = Math.Abs(BitConverter.ToInt32(imageData, 22));
            return (width, height);
        }
        
        // GIF
        if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46)
        {
            int width = imageData[6] | (imageData[7] << 8);
            int height = imageData[8] | (imageData[9] << 8);
            return (width, height);
        }
        
        return null;
    }
    
    private async Task ExtractFileMetadataAsync(string? filePath, ClipboardMetadata metadata)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            
            if (fileInfo.Exists)
            {
                metadata.FileName = fileInfo.Name;
                metadata.FileSize = fileInfo.Length;
                metadata.FileExtension = fileInfo.Extension;
                metadata.OriginalPath = filePath;
                metadata.MimeType = FileTypeHelper.GetMimeType(fileInfo.Extension);
            }
            else
            {
                // File doesn't exist but we have the path
                metadata.FileName = Path.GetFileName(filePath);
                metadata.FileExtension = Path.GetExtension(filePath);
                metadata.OriginalPath = filePath;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error extracting file metadata for: {FilePath}", filePath);
        }
        
        await Task.CompletedTask;
    }
    
    private static void ExtractMultipleFilesMetadata(string[]? filePaths, ClipboardMetadata metadata)
    {
        if (filePaths == null || filePaths.Length == 0) return;
        
        metadata.FileCount = filePaths.Length;
        
        long totalSize = 0;
        foreach (string path in filePaths)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    totalSize += fileInfo.Length;
                }
            }
            catch { /* Ignore individual file errors */ }
        }
        
        metadata.FileSize = totalSize;
        metadata.OriginalPath = string.Join(";", filePaths);
    }
    
    private async Task ExtractVideoMetadataAsync(string? filePath, ClipboardMetadata metadata)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        
        await ExtractFileMetadataAsync(filePath, metadata);
        
        // Note: Duration extraction requires a media library like FFmpeg
        // For now, we just mark the mime type
        if (!string.IsNullOrEmpty(metadata.FileExtension))
        {
            metadata.MimeType = FileTypeHelper.GetMimeType(metadata.FileExtension);
        }
    }
    
    private async Task ExtractAudioMetadataAsync(string? filePath, ClipboardMetadata metadata)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        
        await ExtractFileMetadataAsync(filePath, metadata);
        
        // Note: Duration extraction requires a media library
        if (!string.IsNullOrEmpty(metadata.FileExtension))
        {
            metadata.MimeType = FileTypeHelper.GetMimeType(metadata.FileExtension);
        }
    }
    
}
