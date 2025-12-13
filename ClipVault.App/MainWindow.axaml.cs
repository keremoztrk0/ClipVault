using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClipVault.App.Helpers;
using ClipVault.App.ViewModels;
using ClipVault.App.Views.Dialogs;
using Serilog;

namespace ClipVault.App;

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
        AddGroupDialog dialog = new ();
        AddGroupDialog.AddGroupResult? result = await dialog.ShowDialog<AddGroupDialog.AddGroupResult?>(this);
        
        if (result != null && _viewModel != null)
        {
            await _viewModel.AddGroupAsync(result.Name, result.Color);
        }
    }
    
    private void OnClipboardItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button button || _viewModel == null) return;
        
        PointerPointProperties properties = e.GetCurrentPoint(button).Properties;
        if (!properties.IsRightButtonPressed) return;
        
        // Get the clipboard item from the button's Tag
        if (button.Tag is not ClipboardItemViewModel item) return;
        
        // Select the item first
        _viewModel.SelectItemCommand.Execute(item);
        
        // Create and show context menu
        ContextMenu contextMenu = CreateContextMenu(item);
        contextMenu.Open(button);
        
        e.Handled = true;
    }
    
    private ContextMenu CreateContextMenu(ClipboardItemViewModel item)
    {
        ContextMenu menu = new();
        
        // Move to Group submenu
        MenuItem moveToGroupItem = new()
        {
            Header = "Move to Group",
            Icon = new PathIcon
            {
                Data = Geometry.Parse("M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z"),
                Width = 14,
                Height = 14
            }
        };
        
        // Add "None" option
        MenuItem noneItem = new() { Header = "None (Remove from group)" };
        noneItem.Click += (_, _) => _viewModel?.MoveToGroupCommand.Execute(null);
        moveToGroupItem.Items.Add(noneItem);
        
        // Add separator
        moveToGroupItem.Items.Add(new Separator());
        
        // Add groups
        if (_viewModel != null)
        {
            foreach (GroupViewModel group in _viewModel.AvailableGroups)
            {
                MenuItem groupItem = new()
                {
                    Header = group.Name,
                    Tag = group
                };
                
                // Add color indicator
                if (!string.IsNullOrEmpty(group.Color))
                {
                    try
                    {
                        groupItem.Icon = new Avalonia.Controls.Shapes.Ellipse
                        {
                            Width = 10,
                            Height = 10,
                            Fill = new SolidColorBrush(Color.Parse(group.Color))
                        };
                    }
                    catch
                    {
                        // Ignore color parse errors
                    }
                }
                
                groupItem.Click += (s, _) =>
                {
                    if (s is MenuItem { Tag: GroupViewModel g })
                    {
                        _viewModel?.MoveToGroupCommand.Execute(g);
                    }
                };
                
                moveToGroupItem.Items.Add(groupItem);
            }
        }
        
        menu.Items.Add(moveToGroupItem);
        
        // Separator
        menu.Items.Add(new Separator());
        
        // Delete item
        MenuItem deleteItem = new()
        {
            Header = "Delete",
            Icon = new PathIcon
            {
                Data = Geometry.Parse("M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z"),
                Width = 14,
                Height = 14
            }
        };
        deleteItem.Click += (_, _) => _viewModel?.DeleteItemCommand.Execute(item);
        menu.Items.Add(deleteItem);
        
        return menu;
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
