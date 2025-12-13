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
    /// Whether clipboard monitoring is active.
    /// </summary>
    bool IsMonitoring { get; }
}
