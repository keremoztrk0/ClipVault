using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ClipVault.App.Services;

namespace ClipVault.App.Helpers;

/// <summary>
/// Manages the system tray icon functionality.
/// </summary>
public class TrayIconManager : ITrayIconService
{
    private TrayIcon? _trayIcon;
    private Window? _mainWindow;
    private readonly IWindowFocusManager _focusManager;
    private bool _disposed;
    
    /// <inheritdoc/>
    public event EventHandler? HotkeyPressed;

    public TrayIconManager()
    {
        _focusManager = WindowFocusManagerFactory.Create();
    }
    
    /// <summary>
    /// Sets the main window reference. Must be called before Initialize.
    /// </summary>
    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
    }
    
    /// <inheritdoc/>
    public void Initialize()
    {
        if (_mainWindow == null)
            throw new InvalidOperationException("Main window must be set before initializing tray icon.");
        
        NativeMenu menu = [];
        
        NativeMenuItem showItem = new NativeMenuItem("Show ClipVault");
        showItem.Click += (_, _) => ShowWindow();
        menu.Add(showItem);
        
        menu.Add(new NativeMenuItemSeparator());
        
        NativeMenuItem exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Add(exitItem);
        
        // Load the icon from the main window (which already has it set via XAML)
        WindowIcon? icon = _mainWindow.Icon;
        
        _trayIcon = new TrayIcon
        {
            ToolTipText = "ClipVault - Clipboard Manager",
            Menu = menu,
            IsVisible = true,
            Icon = icon
        };
        
        _trayIcon.Clicked += (_, _) => ToggleWindow();
    }
    
    /// <summary>
    /// Raises the HotkeyPressed event. Called by HotkeyManager.
    /// </summary>
    public void OnHotkeyPressed()
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            ToggleWindow();
        });
    }
    
    /// <inheritdoc/>
    public void ShowWindow()
    {
        if (_mainWindow == null) return;
        
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        
        // Use platform-specific focus manager for reliable focus
        _focusManager.FocusWindow(_mainWindow);
    }
    
    /// <inheritdoc/>
    public void HideWindow()
    {
        _mainWindow?.Hide();
    }
    
    /// <inheritdoc/>
    public void ToggleWindow()
    {
        if (_mainWindow == null) return;
        
        if (_mainWindow.IsVisible)
        {
            HideWindow();
        }
        else
        {
            ShowWindow();
        }
    }
    
    /// <summary>
    /// Exits the application.
    /// </summary>
    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _trayIcon?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
