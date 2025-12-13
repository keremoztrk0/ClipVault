namespace ClipVault.App.Services.Clipboard;

/// <summary>
/// Interface for platform-specific clipboard monitoring.
/// </summary>
public interface IClipboardMonitor : IDisposable
{
    /// <summary>
    /// Fired when clipboard content changes.
    /// </summary>
    event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
    
    /// <summary>
    /// Starts monitoring clipboard changes.
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// Stops monitoring clipboard changes.
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Gets the current clipboard content.
    /// </summary>
    Task<ClipboardContent?> GetCurrentContentAsync();
    
    /// <summary>
    /// Sets content to the clipboard.
    /// </summary>
    Task SetContentAsync(ClipboardContent content);
    
    /// <summary>
    /// Whether monitoring is currently active.
    /// </summary>
    bool IsMonitoring { get; }
}
