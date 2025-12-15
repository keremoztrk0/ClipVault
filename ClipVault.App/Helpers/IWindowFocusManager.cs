using Avalonia.Controls;

namespace ClipVault.App.Helpers;

/// <summary>
/// Interface for platform-specific window focus management.
/// </summary>
public interface IWindowFocusManager
{
    /// <summary>
    /// Forces the window to the foreground and gives it keyboard focus.
    /// </summary>
    /// <param name="window">The window to focus.</param>
    /// <returns>True if the window was successfully focused.</returns>
    bool FocusWindow(Window window);
}
