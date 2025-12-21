using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClipVault.App.Extensions;
using ClipVault.App.Helpers;
using ClipVault.App.Services;
using ClipVault.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipVault.App;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private ITrayIconService? _trayIconService;
    private HotkeyManager? _hotkeyManager;
    private IDialogService? _dialogService;
    private IWindowSettingsService? _windowSettingsService;
    private ILogger<MainWindow>? _logger;
    
    private readonly ScrollViewer? _groupScrollViewer;
    private readonly ScrollViewer? _clipboardListScrollViewer;
    private readonly ItemsControl? _clipboardItemsControl;
    
    public MainWindow()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
        Closing += OnClosing;
        Deactivated += OnDeactivated;
        
        // Use tunneling strategy to intercept keyboard events before child controls handle them
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        
        // Get reference to scroll viewers and items controls for navigation
        _groupScrollViewer = this.FindControl<ScrollViewer>("GroupScrollViewer");
        _clipboardListScrollViewer = this.FindControl<ScrollViewer>("ClipboardListScrollViewer");
        _clipboardItemsControl = this.FindControl<ItemsControl>("ClipboardItemsControl");
    }
    
    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Don't hide if a dialog is open
        if (_dialogService?.IsDialogOpen == true) return;
        
        // Hide window when it loses focus
        _trayIconService?.HideWindow();
    }
    
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Get services from DI container
        _logger = App.Services.GetRequiredService<ILogger<MainWindow>>();
        _dialogService = App.Services.GetRequiredService<IDialogService>();
        _windowSettingsService = App.Services.GetRequiredService<IWindowSettingsService>();
        _hotkeyManager = App.Services.GetRequiredService<HotkeyManager>();
        _viewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        
        // Get tray icon service and set up window reference
        TrayIconManager trayIconManager = App.Services.GetRequiredService<TrayIconManager>();
        trayIconManager.SetMainWindow(this);
        _trayIconService = trayIconManager;
        
        // Set up services that need the window reference
        if (_dialogService is DialogService dialogService)
        {
            dialogService.SetOwnerWindow(this);
        }
        _windowSettingsService.SetTargetWindow(this);
        
        DataContext = _viewModel;
        
        // Subscribe to ViewModel events
        _viewModel.HideWindowRequested += OnHideWindowRequested;
        _viewModel.ScrollItemIntoViewRequested += OnScrollItemIntoViewRequested;
        _viewModel.ScrollGroupIntoViewRequested += OnScrollGroupIntoViewRequested;
        _viewModel.SettingsApplied += OnSettingsApplied;
        
        // Load settings first
        await _windowSettingsService.LoadAndApplySettingsAsync();
        
        // Initialize tray icon
        _trayIconService.Initialize();
        
        // Initialize global hotkey with setting
        string hotkey = _windowSettingsService.CurrentSettings?.GlobalHotkey ?? "Ctrl+Shift+V";
        _hotkeyManager.SetHotkey(hotkey);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        
        // Start hotkey listener in background
        Task.Run(async () =>
        {
            try
            {
                await _hotkeyManager.StartAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Hotkey manager failed to start");
            }
        }).SafeFireAndForget();
        
        await _viewModel.InitializeAsync();
        
        // Apply start minimized setting
        if (_windowSettingsService.CurrentSettings?.StartMinimized == true)
        {
            _trayIconService.HideWindow();
        }
    }
    
    private void OnHideWindowRequested(object? sender, EventArgs e)
    {
        _trayIconService?.HideWindow();
    }
    
    private void OnScrollItemIntoViewRequested(object? sender, int index)
    {
        if (_clipboardItemsControl == null) return;
        
        // Get the container for the specified index
        Control? container = _clipboardItemsControl.ContainerFromIndex(index);
        container?.BringIntoView();
    }
    
    private void OnScrollGroupIntoViewRequested(object? sender, int index)
    {
        if (_groupScrollViewer == null) return;
        
        // Find the group ItemsControl within the scroll viewer
        ItemsControl? groupItemsControl = _groupScrollViewer.FindDescendantOfType<ItemsControl>();
        if (groupItemsControl == null) return;
        
        // Get the container for the specified index
        Control? container = groupItemsControl.ContainerFromIndex(index);
        container?.BringIntoView();
    }
    
    private async void OnSettingsApplied(object? sender, EventArgs e)
    {
        if (_windowSettingsService == null) return;
        
        // Reload and apply all settings (theme, show in taskbar, etc.)
        await _windowSettingsService.LoadAndApplySettingsAsync();
        
        // Update hotkey if changed
        if (_windowSettingsService.CurrentSettings != null && _hotkeyManager != null)
        {
            _hotkeyManager.SetHotkey(_windowSettingsService.CurrentSettings.GlobalHotkey);
            _logger?.LogInformation("Hotkey updated to: {Hotkey}", _windowSettingsService.CurrentSettings.GlobalHotkey);
        }
    }
    
    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            _trayIconService?.ToggleWindow();
        });
    }
    
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Minimize to tray instead of closing
        if (e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            _trayIconService?.HideWindow();
            return;
        }
        
        // Unsubscribe from ViewModel events
        if (_viewModel != null)
        {
            _viewModel.HideWindowRequested -= OnHideWindowRequested;
            _viewModel.ScrollItemIntoViewRequested -= OnScrollItemIntoViewRequested;
            _viewModel.ScrollGroupIntoViewRequested -= OnScrollGroupIntoViewRequested;
            _viewModel.SettingsApplied -= OnSettingsApplied;
        }
        
        // Cleanup
        _viewModel?.Dispose();
        _hotkeyManager?.Dispose();
        _trayIconService?.Dispose();
    }
    
    private async void OnClipboardItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Button button || _viewModel == null) return;
        
        // Get the clipboard item from the button's Tag
        if (button.Tag is not ClipboardItemViewModel item) return;
        
        // Select the item, copy to clipboard, and hide window
        _viewModel.SelectItemCommand.Execute(item);
        await _viewModel.CopyAndHideCommand.ExecuteAsync(null);
        
        e.Handled = true;
    }
    
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;
        
        bool ctrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        
        switch (e.Key)
        {
            // Arrow Up/Down - Navigate clipboard items
            case Key.Up when !ctrlPressed:
                _viewModel.SelectPreviousItemCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Down when !ctrlPressed:
                _viewModel.SelectNextItemCommand.Execute(null);
                e.Handled = true;
                break;
            
            // Left/Right - Navigate groups
            case Key.Left when !ctrlPressed:
                _viewModel.SelectPreviousGroupCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Right when !ctrlPressed:
                _viewModel.SelectNextGroupCommand.Execute(null);
                e.Handled = true;
                break;
            
            // Enter - Copy selected item to clipboard and hide
            case Key.Enter:
                e.Handled = true; // Always handle Enter to prevent button command execution
                if (_viewModel.SelectedItem != null)
                {
                    await _viewModel.CopyAndHideCommand.ExecuteAsync(null);
                }
                break;
            
            // Delete - Delete selected item with confirmation
            case Key.Delete:
                if (_viewModel.SelectedItem != null)
                {
                    await _viewModel.DeleteSelectedItemWithConfirmationCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                break;
            
            // Escape - Hide window
            case Key.Escape:
                _viewModel.HideWindowCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
    
    // Allow closing the app via menu
    public void ForceClose()
    {
        _viewModel?.Dispose();
        _hotkeyManager?.Dispose();
        _trayIconService?.Dispose();
        Close();
    }
}
