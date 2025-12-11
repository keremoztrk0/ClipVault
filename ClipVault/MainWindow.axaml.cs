using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClipVault.Helpers;
using ClipVault.ViewModels;
using ClipVault.Views.Dialogs;
using Serilog;

namespace ClipVault;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private TrayIconManager? _trayIconManager;
    private HotkeyManager? _hotkeyManager;
    
    public MainWindow()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
        Closing += OnClosing;
    }
    
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        
        // Initialize tray icon
        _trayIconManager = new TrayIconManager(this);
        _trayIconManager.Initialize();
        
        // Initialize global hotkey
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.SetHotkey("Ctrl+Shift+Enter");
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        
        // Start hotkey listener in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _hotkeyManager.StartAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Hotkey manager failed to start");
            }
        });
        
        await _viewModel.InitializeAsync();
    }
    
    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            _trayIconManager?.ToggleWindow();
        });
    }
    
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Minimize to tray instead of closing
        if (e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            _trayIconManager?.HideWindow();
            return;
        }
        
        // Cleanup
        _viewModel?.Dispose();
        _hotkeyManager?.Dispose();
        _trayIconManager?.Dispose();
    }
    
    private async void OnAddGroupClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AddGroupDialog();
        var result = await dialog.ShowDialog<AddGroupDialog.AddGroupResult?>(this);
        
        if (result != null && _viewModel != null)
        {
            await _viewModel.AddGroupAsync(result.Name, result.Color);
        }
    }
    
    // Allow closing the app via menu
    public void ForceClose()
    {
        _viewModel?.Dispose();
        _hotkeyManager?.Dispose();
        _trayIconManager?.Dispose();
        Close();
    }
}
