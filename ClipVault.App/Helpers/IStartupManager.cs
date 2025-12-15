namespace ClipVault.App.Helpers;

/// <summary>
/// Interface for managing application startup with system boot.
/// </summary>
public interface IStartupManager
{
    /// <summary>
    /// Gets whether the application is configured to start with the system.
    /// </summary>
    bool IsStartupEnabled { get; }
    
    /// <summary>
    /// Enables the application to start with the system.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    bool EnableStartup();
    
    /// <summary>
    /// Disables the application from starting with the system.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    bool DisableStartup();
    
    /// <summary>
    /// Sets the startup state based on the provided value.
    /// </summary>
    /// <param name="enabled">True to enable startup, false to disable.</param>
    /// <returns>True if the operation was successful.</returns>
    bool SetStartup(bool enabled)
    {
        return enabled ? EnableStartup() : DisableStartup();
    }
}
