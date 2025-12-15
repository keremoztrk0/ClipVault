using Avalonia.Controls;
using Serilog;

namespace ClipVault.App.Helpers;

/// <summary>
/// macOS-specific implementation of window focus management.
/// Uses Avalonia's built-in methods as macOS generally handles window focus better.
/// </summary>
public class MacOsWindowFocusManager : IWindowFocusManager
{
    private static readonly ILogger Logger = Log.ForContext<MacOsWindowFocusManager>();

    /// <inheritdoc />
    public bool FocusWindow(Window window)
    {
        try
        {
            // macOS generally respects Activate() calls better than Windows
            window.Activate();
            window.Focus();
            
            Logger.Debug("FocusWindow called on macOS");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to focus window on macOS");
            return false;
        }
    }
}
