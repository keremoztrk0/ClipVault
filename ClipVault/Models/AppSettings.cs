namespace ClipVault.Models;

/// <summary>
/// Application settings stored in the database.
/// </summary>
public class AppSettings
{
    public int Id { get; set; } = 1;  // Singleton record
    
    /// <summary>
    /// Global hotkey to show/hide the application.
    /// Format: "Ctrl+Shift+Enter" or similar.
    /// </summary>
    public string GlobalHotkey { get; set; } = "Ctrl+Shift+V";
    
    /// <summary>
    /// Theme preference: "System", "Light", or "Dark"
    /// </summary>
    public string Theme { get; set; } = "Light";
    
    /// <summary>
    /// Whether to start the application minimized to tray.
    /// </summary>
    public bool StartMinimized { get; set; } = false;
    
    /// <summary>
    /// Whether to start with Windows/OS startup.
    /// </summary>
    public bool StartWithSystem { get; set; } = false;
    
    /// <summary>
    /// Window opacity (0.0 to 1.0)
    /// </summary>
    public double WindowOpacity { get; set; } = 0.95;
    
    /// <summary>
    /// Maximum number of items to store (0 = unlimited)
    /// </summary>
    public int MaxHistoryItems { get; set; } = 1000;
    
    /// <summary>
    /// Days to keep items before auto-delete (0 = forever)
    /// </summary>
    public int RetentionDays { get; set; } = 0;
    
    /// <summary>
    /// Whether to show in taskbar.
    /// </summary>
    public bool ShowInTaskbar { get; set; } = true;
}
