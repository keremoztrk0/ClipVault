namespace ClipVault.Models;

/// <summary>
/// Stores type-specific metadata for clipboard items.
/// </summary>
public class ClipboardMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid ClipboardItemId { get; set; }
    
    // Text-specific metadata
    public int? CharacterCount { get; set; }
    public int? WordCount { get; set; }
    public int? LineCount { get; set; }
    
    // File-specific metadata
    public string? FileName { get; set; }
    public long? FileSize { get; set; }  // In bytes
    public string? FileExtension { get; set; }
    public string? OriginalPath { get; set; }  // Where the file was copied from
    
    // Media-specific metadata (video/audio)
    public long? DurationMs { get; set; }  // Duration in milliseconds
    
    // Image-specific metadata
    public int? Width { get; set; }
    public int? Height { get; set; }
    
    // Common metadata
    public string? MimeType { get; set; }
    
    // File count for multi-file copies
    public int? FileCount { get; set; }
    
    // Navigation property (populated manually with Dapper)
    public ClipboardItem? ClipboardItem { get; set; }
}
