using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClipVault.App.Data.Repositories;
using ClipVault.App.Helpers;
using ClipVault.App.Models;
using Serilog;

namespace ClipVault.App.ViewModels;

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private static readonly ILogger Logger = Log.ForContext<SettingsViewModel>();
    
    private readonly ISettingsRepository _settingsRepository;
    private readonly IStartupManager _startupManager;
    private AppSettings? _originalSettings;
    
    [ObservableProperty]
    private string _globalHotkey = "Ctrl+Shift+V";
    
    [ObservableProperty]
    private string _selectedTheme = "Light";
    
    [ObservableProperty]
    private bool _startMinimized;
    
    [ObservableProperty]
    private bool _startWithSystem;
    
    [ObservableProperty]
    private double _windowOpacity = 0.95;
    
    [ObservableProperty]
    private int _maxHistoryItems = 1000;
    
    [ObservableProperty]
    private int _retentionDays;
    
    [ObservableProperty]
    private bool _showInTaskbar = true;
    
    [ObservableProperty]
    private bool _hasChanges;
    
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    /// <summary>
    /// Available theme options.
    /// </summary>
    public string[] ThemeOptions { get; } = ["Light", "Dark", "System"];
    
    /// <summary>
    /// Available max history item options.
    /// </summary>
    public int[] MaxHistoryOptions { get; } = [100, 500, 1000, 5000, 10000, 0];
    
    /// <summary>
    /// Available retention day options.
    /// </summary>
    public int[] RetentionDayOptions { get; } = [0, 7, 30, 90, 180, 365];
    
    public SettingsViewModel(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
        _startupManager = StartupManagerFactory.Create();
    }
    
    /// <summary>
    /// Loads settings from the repository.
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        try
        {
            _originalSettings = await _settingsRepository.GetSettingsAsync();
            
            GlobalHotkey = _originalSettings.GlobalHotkey;
            SelectedTheme = _originalSettings.Theme;
            StartMinimized = _originalSettings.StartMinimized;
            
            // Get actual startup state from system (not just from DB)
            bool actualStartupState = _startupManager.IsStartupEnabled;
            StartWithSystem = actualStartupState;
            
            // Sync DB if it's out of sync with system
            if (_originalSettings.StartWithSystem != actualStartupState)
            {
                _originalSettings.StartWithSystem = actualStartupState;
                Logger.Debug("Syncing StartWithSystem state: DB had {DbValue}, system has {SystemValue}", 
                    !actualStartupState, actualStartupState);
            }
            
            WindowOpacity = _originalSettings.WindowOpacity;
            MaxHistoryItems = _originalSettings.MaxHistoryItems;
            RetentionDays = _originalSettings.RetentionDays;
            ShowInTaskbar = _originalSettings.ShowInTaskbar;
            
            HasChanges = false;
            StatusMessage = string.Empty;
            
            Logger.Debug("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load settings");
            StatusMessage = "Failed to load settings";
        }
    }
    
    /// <summary>
    /// Saves the current settings.
    /// </summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            // Apply startup setting to system first
            bool startupChanged = _originalSettings?.StartWithSystem != StartWithSystem;
            if (startupChanged)
            {
                bool success = _startupManager.SetStartup(StartWithSystem);
                if (!success)
                {
                    Logger.Warning("Failed to {Action} startup", StartWithSystem ? "enable" : "disable");
                    StatusMessage = $"Failed to {(StartWithSystem ? "enable" : "disable")} startup with system";
                    // Don't save the setting if we couldn't apply it
                    StartWithSystem = _startupManager.IsStartupEnabled;
                }
                else
                {
                    Logger.Information("Startup {Action}", StartWithSystem ? "enabled" : "disabled");
                }
            }
            
            var settings = new AppSettings
            {
                Id = 1,
                GlobalHotkey = GlobalHotkey,
                Theme = SelectedTheme,
                StartMinimized = StartMinimized,
                StartWithSystem = StartWithSystem,
                WindowOpacity = WindowOpacity,
                MaxHistoryItems = MaxHistoryItems,
                RetentionDays = RetentionDays,
                ShowInTaskbar = ShowInTaskbar
            };
            
            await _settingsRepository.UpdateSettingsAsync(settings);
            _originalSettings = settings;
            HasChanges = false;
            StatusMessage = "Settings saved";
            
            Logger.Information("Settings saved successfully");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save settings");
            StatusMessage = "Failed to save settings";
        }
    }
    
    /// <summary>
    /// Resets settings to their original values.
    /// </summary>
    [RelayCommand]
    private void ResetSettings()
    {
        if (_originalSettings == null) return;
        
        GlobalHotkey = _originalSettings.GlobalHotkey;
        SelectedTheme = _originalSettings.Theme;
        StartMinimized = _originalSettings.StartMinimized;
        StartWithSystem = _originalSettings.StartWithSystem;
        WindowOpacity = _originalSettings.WindowOpacity;
        MaxHistoryItems = _originalSettings.MaxHistoryItems;
        RetentionDays = _originalSettings.RetentionDays;
        ShowInTaskbar = _originalSettings.ShowInTaskbar;
        
        HasChanges = false;
        StatusMessage = "Settings reset";
    }
    
    /// <summary>
    /// Resets all settings to default values.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = new AppSettings();
        
        GlobalHotkey = defaults.GlobalHotkey;
        SelectedTheme = defaults.Theme;
        StartMinimized = defaults.StartMinimized;
        StartWithSystem = defaults.StartWithSystem;
        WindowOpacity = defaults.WindowOpacity;
        MaxHistoryItems = defaults.MaxHistoryItems;
        RetentionDays = defaults.RetentionDays;
        ShowInTaskbar = defaults.ShowInTaskbar;
        
        HasChanges = true;
        StatusMessage = "Reset to defaults - click Save to apply";
    }
    
    partial void OnGlobalHotkeyChanged(string value)
    {
        CheckForChanges();
        
        // Validate hotkey has at least one modifier and a main key
        if (!string.IsNullOrEmpty(value))
        {
            bool hasModifier = value.Contains("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                               value.Contains("Shift", StringComparison.OrdinalIgnoreCase) ||
                               value.Contains("Alt", StringComparison.OrdinalIgnoreCase);
            
            string[] parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries);
            bool hasMainKey = parts.Length > 0 && 
                              !parts[^1].Equals("Ctrl", StringComparison.OrdinalIgnoreCase) &&
                              !parts[^1].Equals("Shift", StringComparison.OrdinalIgnoreCase) &&
                              !parts[^1].Equals("Alt", StringComparison.OrdinalIgnoreCase);
            
            if (!hasModifier || !hasMainKey)
            {
                StatusMessage = "Hotkey must have a modifier (Ctrl/Shift/Alt) and a key";
            }
        }
    }
    partial void OnSelectedThemeChanged(string value) => CheckForChanges();
    partial void OnStartMinimizedChanged(bool value) => CheckForChanges();
    partial void OnStartWithSystemChanged(bool value) => CheckForChanges();
    partial void OnWindowOpacityChanged(double value) => CheckForChanges();
    partial void OnMaxHistoryItemsChanged(int value) => CheckForChanges();
    partial void OnRetentionDaysChanged(int value) => CheckForChanges();
    partial void OnShowInTaskbarChanged(bool value) => CheckForChanges();
    
    private void CheckForChanges()
    {
        if (_originalSettings == null)
        {
            HasChanges = false;
            return;
        }
        
        HasChanges = GlobalHotkey != _originalSettings.GlobalHotkey ||
                     SelectedTheme != _originalSettings.Theme ||
                     StartMinimized != _originalSettings.StartMinimized ||
                     StartWithSystem != _originalSettings.StartWithSystem ||
                     Math.Abs(WindowOpacity - _originalSettings.WindowOpacity) > 0.001 ||
                     MaxHistoryItems != _originalSettings.MaxHistoryItems ||
                     RetentionDays != _originalSettings.RetentionDays ||
                     ShowInTaskbar != _originalSettings.ShowInTaskbar;
    }
    
    /// <summary>
    /// Gets the display text for max history items.
    /// </summary>
    public static string GetMaxHistoryDisplayText(int value)
    {
        return value == 0 ? "Unlimited" : value.ToString("N0");
    }
    
    /// <summary>
    /// Gets the display text for retention days.
    /// </summary>
    public static string GetRetentionDisplayText(int value)
    {
        return value == 0 ? "Forever" : $"{value} days";
    }
}
