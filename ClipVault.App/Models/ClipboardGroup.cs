namespace ClipVault.App.Models;

/// <summary>
/// Represents a user-defined group for organizing clipboard items.
/// </summary>
public class ClipboardGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Hex color code for visual identification (e.g., "#FF5733")
    /// </summary>
    public string Color { get; set; } = "#3498db";
    
    /// <summary>
    /// Order in which groups appear in the UI
    /// </summary>
    public int SortOrder { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property (populated manually with Dapper)
    public ICollection<ClipboardItem> Items { get; set; } = new List<ClipboardItem>();
}
