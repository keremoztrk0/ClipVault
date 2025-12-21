using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using ClipVault.App.Controls;
using ClipVault.App.Data.Repositories;
using ClipVault.App.Helpers;
using ClipVault.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ClipVault.App.Views;

public partial class SettingsWindow : Window
{
    private readonly HotkeyManager? _hotkeyManager;
    private HotkeyRecorder? _hotkeyRecorder;
    
    /// <summary>
    /// Converter for displaying max history items (0 = Unlimited).
    /// </summary>
    public static readonly FuncValueConverter<int, string> MaxHistoryConverter =
        new(value => value == 0 ? "Unlimited" : value.ToString("N0"));
    
    /// <summary>
    /// Converter for displaying retention days (0 = Forever).
    /// </summary>
    public static readonly FuncValueConverter<int, string> RetentionDaysConverter =
        new(value => value == 0 ? "Forever" : $"{value} days");
    
    public SettingsWindow()
    {
        InitializeComponent();
    }
    
    public SettingsWindow(HotkeyManager? hotkeyManager = null)
    {
        InitializeComponent();
        
        // Get SettingsViewModel from DI (already has logger and repository injected)
        try
        {
            DataContext = App.Services.GetRequiredService<SettingsViewModel>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to resolve SettingsViewModel from DI: {ex}");
            throw;
        }
        
        _hotkeyManager = hotkeyManager;
        
        // Wire up hotkey recorder events after control is loaded
        Loaded += OnWindowLoaded;
    }
    
    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        _hotkeyRecorder = this.FindControl<HotkeyRecorder>("HotkeyRecorderControl");
        
        if (_hotkeyRecorder != null && _hotkeyManager != null)
        {
            _hotkeyRecorder.RecordingStarted += OnHotkeyRecordingStarted;
            _hotkeyRecorder.RecordingStopped += OnHotkeyRecordingStopped;
        }
    }
    
    private void OnHotkeyRecordingStarted(object? sender, EventArgs e)
    {
        // Suspend global hotkey detection while recording
        _hotkeyManager?.Suspend();
    }
    
    private void OnHotkeyRecordingStopped(object? sender, EventArgs e)
    {
        // Resume global hotkey detection after recording
        _hotkeyManager?.Resume();
    }
    
    /// <summary>
    /// Initializes and shows the settings window.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.LoadSettingsAsync();
        }
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
    
    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            // Wait for save to complete before closing
            await viewModel.SaveSettingsCommand.ExecuteAsync(null);
        }
        Close(true);
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Clean up event handlers
        if (_hotkeyRecorder != null)
        {
            _hotkeyRecorder.RecordingStarted -= OnHotkeyRecordingStarted;
            _hotkeyRecorder.RecordingStopped -= OnHotkeyRecordingStopped;
        }
        
        // Ensure hotkey manager is resumed if window is closed during recording
        _hotkeyManager?.Resume();
        
        base.OnClosed(e);
    }
}
