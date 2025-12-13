namespace ClipVault.App.Services.Clipboard;

/// <summary>
/// Factory for creating platform-specific clipboard monitors.
/// </summary>
public static class ClipboardMonitorFactory
{
    /// <summary>
    /// Creates a clipboard monitor for the current platform.
    /// </summary>
    public static IClipboardMonitor Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsClipboardMonitor();
        }
        
        if (OperatingSystem.IsMacOS())
        {
            return new MacOsClipboardMonitor();
        }
        
        if (OperatingSystem.IsLinux())
        {
            return new LinuxClipboardMonitor();
        }
        
        throw new PlatformNotSupportedException(
            $"Clipboard monitoring is not supported on {Environment.OSVersion.Platform}");
    }
}
