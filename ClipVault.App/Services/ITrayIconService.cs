namespace ClipVault.App.Services;

/// <summary>
/// Service interface for managing the system tray icon.
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Initializes the tray icon.
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Shows the main window and gives it keyboard focus.
    /// </summary>
    void ShowWindow();
    
    /// <summary>
    /// Hides the main window to tray.
    /// </summary>
    void HideWindow();
    
    /// <summary>
    /// Toggles window visibility.
    /// </summary>
    void ToggleWindow();
    
    /// <summary>
    /// Event raised when the hotkey is pressed.
    /// </summary>
    event EventHandler? HotkeyPressed;
}
