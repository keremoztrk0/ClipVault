using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using ClipVault.App.Data.Repositories;
using ClipVault.App.Models;
using Microsoft.Extensions.Logging;

namespace ClipVault.App.Services;

/// <summary>
/// Service interface for managing window settings like theme.
/// </summary>
public interface IWindowSettingsService
{
    /// <summary>
    /// Gets the current settings.
    /// </summary>
    AppSettings? CurrentSettings { get; }
    
    /// <summary>
    /// Loads and applies settings from the repository.
    /// </summary>
    Task LoadAndApplySettingsAsync();
    
    /// <summary>
    /// Applies the given settings.
    /// </summary>
    void ApplySettings(AppSettings settings);
    
    /// <summary>
    /// Sets the target window for settings application.
    /// </summary>
    void SetTargetWindow(Window window);
}

/// <summary>
/// Service implementation for managing window settings.
/// </summary>
public class WindowSettingsService(ILogger<WindowSettingsService> logger, ISettingsRepository settingsRepository)
    : IWindowSettingsService
{
    private Window? _targetWindow;
    
    /// <inheritdoc/>
    public AppSettings? CurrentSettings { get; private set; }

    /// <inheritdoc/>
    public void SetTargetWindow(Window window)
    {
        _targetWindow = window;
    }
    
    /// <inheritdoc/>
    public async Task LoadAndApplySettingsAsync()
    {
        try
        {
            CurrentSettings = await settingsRepository.GetSettingsAsync();
            ApplySettings(CurrentSettings);
            logger.LogInformation("Settings loaded and applied: Hotkey={Hotkey}, Theme={Theme}", 
                CurrentSettings.GlobalHotkey, CurrentSettings.Theme);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load settings, using defaults");
            CurrentSettings = new AppSettings();
            ApplySettings(CurrentSettings);
        }
    }
    
    /// <inheritdoc/>
    public void ApplySettings(AppSettings settings)
    {
        if (_targetWindow == null)
        {
            logger.LogWarning("Cannot apply settings: target window not set");
            return;
        }
        
        // Apply theme first (affects colors)
        ApplyTheme(settings.Theme);
        
        // Apply show in taskbar
        _targetWindow.ShowInTaskbar = settings.ShowInTaskbar;
        
        CurrentSettings = settings;
        
        logger.LogDebug("Applied settings: Theme={Theme}, ShowInTaskbar={ShowInTaskbar}", 
            settings.Theme, settings.ShowInTaskbar);
    }
    
    /// <summary>
    /// Applies the theme setting.
    /// </summary>
    private void ApplyTheme(string theme)
    {
        try
        {
            ThemeVariant themeVariant = theme switch
            {
                "Dark" => ThemeVariant.Dark,
                "Light" => ThemeVariant.Light,
                _ => ThemeVariant.Default // System
            };
            
            // Apply to Application level so dialogs inherit the theme
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = themeVariant;
            }
            
            // Also apply to target window for immediate effect
            if (_targetWindow != null)
            {
                _targetWindow.RequestedThemeVariant = themeVariant;
            }
            
            logger.LogDebug("Applied theme: {Theme}", theme);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply theme {Theme}", theme);
        }
    }
}
