namespace ClipVault.Models;

/// <summary>
/// Represents a single clipboard entry stored in the database.
/// </summary>
public class ClipboardItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public ClipboardContentType ContentType { get; set; }
    
    /// <summary>
    /// The actual text content for text-based clips.
    /// </summary>
    public string? TextContent { get; set; }
    
    /// <summary>
    /// Path to the stored file for binary content (images, files, etc.)
    /// Stored in app data folder.
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// First N characters of content for quick display and search.
    /// </summary>
    public string? PreviewText { get; set; }
    
    /// <summary>
    /// Name of the application from which the content was copied.
    /// </summary>
    public string? SourceApplication { get; set; }
    
    /// <summary>
    /// Optional group assignment.
    /// </summary>
    public Guid? GroupId { get; set; }
    
    /// <summary>
    /// Whether this item is marked as favorite.
    /// </summary>
    public bool IsFavorite { get; set; }
    
    /// <summary>
    /// When the item was first copied to clipboard.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the item was last accessed (copied back to clipboard).
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Hash of the content to detect duplicates.
    /// </summary>
    public string? ContentHash { get; set; }
    
    // Navigation properties (populated manually with Dapper)
    public ClipboardGroup? Group { get; set; }
    public ClipboardMetadata? Metadata { get; set; }
}
