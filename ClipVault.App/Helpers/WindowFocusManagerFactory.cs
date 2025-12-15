using System.Runtime.InteropServices;

namespace ClipVault.App.Helpers;

/// <summary>
/// Factory for creating platform-specific window focus manager instances.
/// </summary>
public static class WindowFocusManagerFactory
{
    /// <summary>
    /// Creates the appropriate IWindowFocusManager for the current platform.
    /// </summary>
    /// <returns>A platform-specific window focus manager.</returns>
    public static IWindowFocusManager Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsWindowFocusManager();
        }
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOsWindowFocusManager();
        }
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxWindowFocusManager();
        }
        
        // Fallback to Linux implementation for unknown platforms
        return new LinuxWindowFocusManager();
    }
}
