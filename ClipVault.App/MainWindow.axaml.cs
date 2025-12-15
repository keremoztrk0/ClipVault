using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ClipVault.App.Data;
using ClipVault.App.Data.Repositories;
using ClipVault.App.Helpers;
using ClipVault.App.Models;
using ClipVault.App.ViewModels;
using ClipVault.App.Views;
using ClipVault.App.Views.Dialogs;
using Serilog;

namespace ClipVault.App;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private TrayIconManager? _trayIconManager;
    private HotkeyManager? _hotkeyManager;
    private bool _isDialogOpen;
    private readonly ScrollViewer? _groupScrollViewer;
    private readonly ScrollViewer? _clipboardListScrollViewer;
    private readonly ItemsControl? _clipboardItemsControl;
    private AppSettings? _currentSettings;
    
    public MainWindow()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
        Closing += OnClosing;
        Deactivated += OnDeactivated;
        
        // Use tunneling strategy to intercept keyboard events before child controls handle them
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        
        // Get reference to scroll viewers and items controls for navigation
        _groupScrollViewer = this.FindControl<ScrollViewer>("GroupScrollViewer");
        if (_groupScrollViewer != null)
        {
            _groupScrollViewer.PointerWheelChanged += OnGroupScrollViewerPointerWheelChanged;
        }
        
        _clipboardListScrollViewer = this.FindControl<ScrollViewer>("ClipboardListScrollViewer");
        _clipboardItemsControl = this.FindControl<ItemsControl>("ClipboardItemsControl");
    }
    
    private void OnGroupScrollViewerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_groupScrollViewer == null) return;
        
        // Convert vertical scroll to horizontal scroll
        double delta = e.Delta.Y * 50; // Adjust scroll speed
        _groupScrollViewer.Offset = new Avalonia.Vector(
            _groupScrollViewer.Offset.X - delta, 
            _groupScrollViewer.Offset.Y);
        
        e.Handled = true;
    }
    
    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Don't hide if a dialog is open
        if (_isDialogOpen) return;
        
        // Hide window when it loses focus
        _trayIconManager?.HideWindow();
    }
    
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        
        // Load settings first
        await LoadAndApplySettingsAsync();
        
        // Initialize tray icon
        _trayIconManager = new TrayIconManager(this);
        _trayIconManager.Initialize();
        
        // Initialize global hotkey with setting
        _hotkeyManager = new HotkeyManager();
        string hotkey = _currentSettings?.GlobalHotkey ?? "Ctrl+Shift+V";
        _hotkeyManager.SetHotkey(hotkey);
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
        
        // Apply start minimized setting
        if (_currentSettings?.StartMinimized == true)
        {
            _trayIconManager.HideWindow();
        }
    }
    
    /// <summary>
    /// Loads settings from the database and applies them to the window.
    /// </summary>
    private async Task LoadAndApplySettingsAsync()
    {
        try
        {
            using AppDbContext dbContext = new();
            ISettingsRepository settingsRepository = new SettingsRepository(dbContext);
            _currentSettings = await settingsRepository.GetSettingsAsync();
            
            ApplySettings(_currentSettings);
            Log.Information("Settings loaded and applied: Hotkey={Hotkey}, Theme={Theme}, Opacity={Opacity}", 
                _currentSettings.GlobalHotkey, _currentSettings.Theme, _currentSettings.WindowOpacity);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings, using defaults");
            _currentSettings = new AppSettings();
            ApplySettings(_currentSettings);
        }
    }
    
    /// <summary>
    /// Applies the given settings to the window.
    /// </summary>
    private void ApplySettings(AppSettings settings)
    {
        // Apply theme first (affects colors)
        ApplyTheme(settings.Theme);
        
        // Apply window opacity
        Opacity = settings.WindowOpacity;
        
        // Apply show in taskbar
        ShowInTaskbar = settings.ShowInTaskbar;
        
        Log.Debug("Applied settings: Theme={Theme}, Opacity={Opacity}, ShowInTaskbar={ShowInTaskbar}", 
            settings.Theme, settings.WindowOpacity, settings.ShowInTaskbar);
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
            
            // Also apply to this window for immediate effect
            RequestedThemeVariant = themeVariant;
            
            Log.Debug("Applied theme: {Theme}", theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply theme {Theme}", theme);
        }
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
        _isDialogOpen = true;
        try
        {
            AddGroupDialog dialog = new();
            AddGroupDialog.AddGroupResult? result = await dialog.ShowDialog<AddGroupDialog.AddGroupResult?>(this);
            
            if (result != null && _viewModel != null)
            {
                await _viewModel.AddGroupAsync(result.Name, result.Color);
            }
        }
        finally
        {
            _isDialogOpen = false;
        }
    }
    
    private void OnClipboardItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button button || _viewModel == null) return;
        
        // Get the clipboard item from the button's Tag
        if (button.Tag is not ClipboardItemViewModel item) return;
        
        PointerPointProperties properties = e.GetCurrentPoint(button).Properties;
        
        if (properties.IsRightButtonPressed)
        {
            // Right-click: Select the item and show context menu
            _viewModel.SelectItemCommand.Execute(item);
            
            ContextMenu contextMenu = CreateContextMenu(item);
            contextMenu.Open(button);
            
            e.Handled = true;
        }
    }
    
    private async void OnClipboardItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Button button || _viewModel == null) return;
        
        // Get the clipboard item from the button's Tag
        if (button.Tag is not ClipboardItemViewModel item) return;
        
        // Select the item, copy to clipboard, and hide window
        _viewModel.SelectItemCommand.Execute(item);
        await _viewModel.CopySelectedItemCommand.ExecuteAsync(null);
        _trayIconManager?.HideWindow();
        
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
        deleteItem.Click += async (_, _) => await DeleteItemWithConfirmationAsync(item);
        menu.Items.Add(deleteItem);
        
        return menu;
    }
    
    private async Task DeleteItemWithConfirmationAsync(ClipboardItemViewModel item)
    {
        _isDialogOpen = true;
        try
        {
            string previewText = item.PreviewText ?? string.Empty;
            string preview = previewText.Length > 50 
                ? previewText[..50] + "..." 
                : previewText;
            
            ConfirmDialog dialog = new(
                "Delete Item",
                $"Are you sure you want to delete this item?\n\n\"{preview}\"",
                "Delete");
            
            bool confirmed = await dialog.ShowDialog<bool>(this);
            
            if (confirmed)
            {
                _viewModel?.DeleteItemCommand.Execute(item);
            }
        }
        finally
        {
            _isDialogOpen = false;
        }
    }
    
    private void OnGroupPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button button || _viewModel == null) return;
        
        // Get the group from the button's Tag
        if (button.Tag is not GroupViewModel group) return;
        
        PointerPointProperties properties = e.GetCurrentPoint(button).Properties;
        
        if (properties.IsRightButtonPressed)
        {
            // Don't allow deleting the "All" group
            if (group.Id == Guid.Empty) return;
            
            // Select the group first
            _viewModel.SelectGroupCommand.Execute(group);
            
            // Create and show context menu
            ContextMenu contextMenu = CreateGroupContextMenu(group);
            contextMenu.Open(button);
            
            e.Handled = true;
        }
    }
    
    private ContextMenu CreateGroupContextMenu(GroupViewModel group)
    {
        ContextMenu menu = new();
        
        // Delete group
        MenuItem deleteItem = new()
        {
            Header = "Delete Group",
            Icon = new PathIcon
            {
                Data = Geometry.Parse("M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z"),
                Width = 14,
                Height = 14
            }
        };
        deleteItem.Click += async (_, _) => await DeleteGroupWithConfirmationAsync(group);
        menu.Items.Add(deleteItem);
        
        return menu;
    }
    
    private async Task DeleteGroupWithConfirmationAsync(GroupViewModel group)
    {
        _isDialogOpen = true;
        try
        {
            ConfirmDialog dialog = new(
                "Delete Group",
                $"Are you sure you want to delete the group \"{group.Name}\"?\n\nItems in this group will not be deleted, they will be moved to \"All\".",
                "Delete");
            
            bool confirmed = await dialog.ShowDialog<bool>(this);
            
            if (confirmed)
            {
                _viewModel?.DeleteGroupCommand.Execute(group);
            }
        }
        finally
        {
            _isDialogOpen = false;
        }
    }
    
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;
        
        bool ctrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        
        switch (e.Key)
        {
            // Arrow Up/Down - Navigate clipboard items
            case Key.Up when !ctrlPressed:
                _viewModel.SelectPreviousItem();
                ScrollSelectedItemIntoView();
                e.Handled = true;
                break;
                
            case Key.Down when !ctrlPressed:
                _viewModel.SelectNextItem();
                ScrollSelectedItemIntoView();
                e.Handled = true;
                break;
            
            // Left/Right - Navigate groups
            case Key.Left when !ctrlPressed:
                _viewModel.SelectPreviousGroup();
                ScrollSelectedGroupIntoView();
                e.Handled = true;
                break;
                
            case Key.Right when !ctrlPressed:
                _viewModel.SelectNextGroup();
                ScrollSelectedGroupIntoView();
                e.Handled = true;
                break;
            
            // Enter - Copy selected item to clipboard and hide
            case Key.Enter:
                e.Handled = true; // Always handle Enter to prevent button command execution
                if (_viewModel.SelectedItem != null)
                {
                    await _viewModel.CopySelectedItemCommand.ExecuteAsync(null);
                    _trayIconManager?.HideWindow();
                }
                break;
            
            // Delete - Delete selected item with confirmation
            case Key.Delete:
                if (_viewModel.SelectedItem != null)
                {
                    await DeleteItemWithConfirmationAsync(_viewModel.SelectedItem);
                    e.Handled = true;
                }

                break;
            
            // Escape - Hide window
            case Key.Escape:
                _trayIconManager?.HideWindow();
                e.Handled = true;
                break;

        }
    }
    
    /// <summary>
    /// Scrolls the currently selected clipboard item into view.
    /// </summary>
    private void ScrollSelectedItemIntoView()
    {
        if (_viewModel?.SelectedItem == null || _clipboardListScrollViewer == null || _clipboardItemsControl == null)
            return;
        
        int selectedIndex = _viewModel.ClipboardItems.IndexOf(_viewModel.SelectedItem);
        if (selectedIndex < 0) return;
        
        // Get the container for the selected item
        Control? container = _clipboardItemsControl.ContainerFromIndex(selectedIndex);
        if (container == null) return;
        
        // Use BringIntoView to scroll the item into view
        container.BringIntoView();
    }
    
    /// <summary>
    /// Scrolls the currently selected group into view.
    /// </summary>
    private void ScrollSelectedGroupIntoView()
    {
        if (_viewModel?.SelectedGroup == null || _groupScrollViewer == null)
            return;
        
        // Find the group ItemsControl within the scroll viewer
        ItemsControl? groupItemsControl = _groupScrollViewer.FindDescendantOfType<ItemsControl>();
        if (groupItemsControl == null) return;
        
        int selectedIndex = _viewModel.Groups.IndexOf(_viewModel.SelectedGroup);
        if (selectedIndex < 0) return;
        
        // Get the container for the selected group
        Control? container = groupItemsControl.ContainerFromIndex(selectedIndex);
        if (container == null) return;
        
        // Use BringIntoView to scroll the group into view
        container.BringIntoView();
    }
    
    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        _isDialogOpen = true;
        try
        {
            // Create a new DbContext and repository for the settings window
            // Note: Don't use 'using' here - the context needs to stay alive during the dialog
            AppDbContext dbContext = new();
            ISettingsRepository settingsRepository = new SettingsRepository(dbContext);
            
            try
            {
                SettingsWindow settingsWindow = new(settingsRepository);
                await settingsWindow.InitializeAsync();
                
                bool? result = await settingsWindow.ShowDialog<bool?>(this);
                
                if (result == true)
                {
                    // Reload and apply settings
                    AppSettings? oldSettings = _currentSettings;
                    await LoadAndApplySettingsAsync();
                    
                    // Tell ViewModel to reload its settings for cleanup
                    await _viewModel!.ReloadSettingsAsync();
                    
                    // Check if hotkey changed - need to update HotkeyManager
                    if (_currentSettings != null && _hotkeyManager != null && 
                        oldSettings?.GlobalHotkey != _currentSettings.GlobalHotkey)
                    {
                        _hotkeyManager.SetHotkey(_currentSettings.GlobalHotkey);
                        Log.Information("Hotkey updated to: {Hotkey}", _currentSettings.GlobalHotkey);
                    }
                    
                    _viewModel!.StatusMessage = "Settings saved and applied";
                }
            }
            finally
            {
                // Dispose the context after the dialog is closed
                dbContext.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open settings window");
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = "Failed to open settings";
            }
        }
        finally
        {
            _isDialogOpen = false;
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
