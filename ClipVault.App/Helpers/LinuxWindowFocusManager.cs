using Avalonia.Controls;
using Serilog;

namespace ClipVault.App.Helpers;

/// <summary>
/// Linux-specific implementation of window focus management.
/// Uses Avalonia's built-in methods. May need X11/Wayland specific implementations in the future.
/// </summary>
public class LinuxWindowFocusManager : IWindowFocusManager
{
    private static readonly ILogger Logger = Log.ForContext<LinuxWindowFocusManager>();

    /// <inheritdoc />
    public bool FocusWindow(Window window)
    {
        try
        {
            // Linux window managers vary in behavior
            // Avalonia's Activate should work for most cases
            window.Activate();
            window.Focus();
            
            Logger.Debug("FocusWindow called on Linux");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to focus window on Linux");
            return false;
        }
    }
}
