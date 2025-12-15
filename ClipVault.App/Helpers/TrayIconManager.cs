using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ClipVault.App.Helpers;

/// <summary>
/// Manages the system tray icon functionality.
/// </summary>
public class TrayIconManager : IDisposable
{
    private TrayIcon? _trayIcon;
    private readonly Window _mainWindow;
    private readonly IWindowFocusManager _focusManager;
    private bool _disposed;
    
    public TrayIconManager(Window mainWindow)
    {
        _mainWindow = mainWindow;
        _focusManager = WindowFocusManagerFactory.Create();

    }
    
    /// <summary>
    /// Initializes the tray icon.
    /// </summary>
    public void Initialize()
    {
        NativeMenu menu = [];
        
        NativeMenuItem showItem = new NativeMenuItem("Show ClipVault");
        showItem.Click += (_, _) => ShowWindow();
        menu.Add(showItem);
        
        menu.Add(new NativeMenuItemSeparator());
        
        NativeMenuItem exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Add(exitItem);
        
        _trayIcon = new TrayIcon
        {
            ToolTipText = "ClipVault - Clipboard Manager",
            Menu = menu,
            IsVisible = true
        };
        
        _trayIcon.Clicked += (_, _) => ToggleWindow();
    }
    
    /// <summary>
    /// Shows the main window and gives it keyboard focus using platform-specific methods.
    /// </summary>
    public void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        
        // Use platform-specific focus manager for reliable focus
        _focusManager.FocusWindow(_mainWindow);
    }
    
    /// <summary>
    /// Hides the main window to tray.
    /// </summary>
    public void HideWindow()
    {
        _mainWindow.Hide();
    }
    
    /// <summary>
    /// Toggles window visibility.
    /// </summary>
    public void ToggleWindow()
    {
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
