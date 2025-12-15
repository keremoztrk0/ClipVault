using ClipVault.App.Models;

namespace ClipVault.App.Services;

/// <summary>
/// High-level interface for clipboard operations.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Fired when a new clipboard item is saved.
    /// </summary>
    event EventHandler<ClipboardItem>? ItemAdded;
    
    /// <summary>
    /// Starts monitoring clipboard changes.
    /// </summary>
    Task StartMonitoringAsync();
    
    /// <summary>
    /// Stops monitoring clipboard changes.
    /// </summary>
    Task StopMonitoringAsync();
    
    /// <summary>
    /// Copies a stored item back to the system clipboard.
    /// </summary>
    Task CopyToClipboardAsync(ClipboardItem item);
    
    /// <summary>
    /// Deletes a clipboard item and its associated files from storage.
    /// </summary>
    /// <param name="item">The item to delete.</param>
    Task DeleteItemAsync(ClipboardItem item);
    
    /// <summary>
    /// Whether clipboard monitoring is active.
    /// </summary>
    bool IsMonitoring { get; }
    
    /// <summary>
    /// Runs cleanup based on settings (max items and retention days).
    /// Items in groups are excluded from cleanup.
    /// </summary>
    /// <param name="maxItems">Maximum number of items to keep (0 = unlimited).</param>
    /// <param name="retentionDays">Delete items older than this many days (0 = keep forever).</param>
    /// <returns>Total number of items deleted.</returns>
    Task<int> CleanupAsync(int maxItems, int retentionDays);
}
