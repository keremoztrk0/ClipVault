using System.Runtime.Versioning;
using Microsoft.Win32;
using Serilog;

namespace ClipVault.App.Helpers;

/// <summary>
/// Windows implementation of startup manager using the registry.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsStartupManager : IStartupManager
{
    private static readonly ILogger Logger = Log.ForContext<WindowsStartupManager>();
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ClipVault";
    
    /// <summary>
    /// Gets the path to the current executable.
    /// </summary>
    private static string ExecutablePath => Environment.ProcessPath ?? 
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClipVault.App.exe");
    
    public bool IsStartupEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                string? value = key?.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(value) && File.Exists(value.Trim('"'));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to check startup status");
                return false;
            }
        }
    }
    
    public bool EnableStartup()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null)
            {
                Logger.Error("Failed to open registry key for writing");
                return false;
            }
            
            // Quote the path to handle spaces
            string quotedPath = $"\"{ExecutablePath}\"";
            key.SetValue(AppName, quotedPath);
            
            Logger.Information("Startup enabled: {Path}", quotedPath);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to enable startup");
            return false;
        }
    }
    
    public bool DisableStartup()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null)
            {
                Logger.Error("Failed to open registry key for writing");
                return false;
            }
            
            // Only delete if it exists
            if (key.GetValue(AppName) != null)
            {
                key.DeleteValue(AppName, false);
                Logger.Information("Startup disabled");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to disable startup");
            return false;
        }
    }
}
