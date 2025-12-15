using System.Runtime.InteropServices;
using Serilog;

namespace ClipVault.App.Helpers;

/// <summary>
/// Factory for creating platform-specific startup managers.
/// </summary>
public static class StartupManagerFactory
{
    private static readonly ILogger Logger = Log.ForContext(typeof(StartupManagerFactory));
    
    /// <summary>
    /// Creates the appropriate startup manager for the current platform.
    /// </summary>
    public static IStartupManager Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Logger.Debug("Creating Windows startup manager");
            return new WindowsStartupManager();
        }
        
        // For now, return a no-op manager for unsupported platforms
        // TODO: Implement Linux and macOS startup managers
        Logger.Warning("Startup management not implemented for this platform");
        return new NoOpStartupManager();
    }
}

/// <summary>
/// No-op startup manager for unsupported platforms.
/// </summary>
internal class NoOpStartupManager : IStartupManager
{
    public bool IsStartupEnabled => false;
    
    public bool EnableStartup()
    {
        Log.Warning("Startup management not supported on this platform");
        return false;
    }
    
    public bool DisableStartup()
    {
        return true; // Always "succeeds" since there's nothing to disable
    }
}
