using System.Collections.ObjectModel;
using ClipVault.App.Data;
using ClipVault.App.Data.Repositories;
using ClipVault.App.Models;
using ClipVault.App.Services;
using ClipVault.App.Services.Clipboard;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ClipVault.App.ViewModels;

/// <summary>
/// Main ViewModel that orchestrates the entire application.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IClipboardRepository _clipboardRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IClipboardService _clipboardService;
    private AppSettings? _currentSettings;
    private bool _disposed;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private ClipboardContentType? _filterContentType;
    
    [ObservableProperty]
    private GroupViewModel? _selectedGroup;
    
    [ObservableProperty]
    private ClipboardItemViewModel? _selectedItem;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string _statusMessage = "Ready";
    
    public ObservableCollection<ClipboardItemViewModel> ClipboardItems { get; } = [];
    public ObservableCollection<GroupViewModel> Groups { get; } = [];
    
    /// <summary>
    /// Groups available for moving items (excludes the "All" pseudo-group).
    /// </summary>
    public IEnumerable<GroupViewModel> AvailableGroups => Groups.Where(g => g.Id != Guid.Empty);
    
    public ClipboardDetailViewModel DetailViewModel { get; }
    
    public MainWindowViewModel()
    {
        Log.Debug("MainWindowViewModel constructor starting");
        
        // Initialize database context
        _dbContext = new AppDbContext();
        
        // Initialize repositories
        _clipboardRepository = new ClipboardRepository(_dbContext);
        _groupRepository = new GroupRepository(_dbContext);
        _settingsRepository = new SettingsRepository(_dbContext);
        
        // Initialize clipboard service
        IClipboardMonitor monitor = ClipboardMonitorFactory.Create();
        Log.Debug("Created clipboard monitor: {MonitorType}", monitor.GetType().Name);
        
        MetadataExtractor metadataExtractor = new MetadataExtractor();
        _clipboardService = new ClipboardService(monitor, _clipboardRepository, metadataExtractor);
        _clipboardService.ItemAdded += OnClipboardItemAdded;
        Log.Debug("Subscribed to ItemAdded event");
        
        // Initialize detail view model
        DetailViewModel = new ClipboardDetailViewModel();
        DetailViewModel.CopyRequested += OnCopyRequested;
        DetailViewModel.DeleteRequested += OnDeleteRequested;
        DetailViewModel.GroupChangeRequested += OnGroupChangeRequested;
        
        Log.Debug("MainWindowViewModel constructor completed");
    }
    
    public async Task InitializeAsync()
    {
        Log.Information("Initializing application");
        IsLoading = true;
        StatusMessage = "Loading...";
        
        try
        {
            // Initialize database schema
            await DatabaseInitializer.InitializeAsync(_dbContext);
            
            // Load settings for cleanup
            _currentSettings = await _settingsRepository.GetSettingsAsync();
            
            // Run cleanup based on settings (max items and retention)
            await RunCleanupAsync();
            
            // Load groups
            await LoadGroupsAsync();
            
            // Load clipboard items
            await LoadClipboardItemsAsync();
            
            // Start clipboard monitoring
            Log.Debug("Starting clipboard monitoring");
            await _clipboardService.StartMonitoringAsync();
            Log.Information("Clipboard monitoring started successfully");
            
            StatusMessage = "Monitoring clipboard";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Log.Error(ex, "Initialization failed");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task LoadGroupsAsync()
    {
        List<ClipboardGroup> groups = await _groupRepository.GetAllAsync();
        int totalCount = await _clipboardRepository.GetCountAsync();
        
        Groups.Clear();
        Groups.Add(GroupViewModel.CreateAllGroup(totalCount));
        
        foreach (ClipboardGroup group in groups)
        {
            Groups.Add(new GroupViewModel(group));
        }
        
        SelectedGroup = Groups.FirstOrDefault();
    }
    
    private async Task LoadClipboardItemsAsync()
    {
        // Remember currently selected item ID to restore selection after reload
        Guid? previouslySelectedId = SelectedItem?.Id;
        
        IEnumerable<ClipboardItem> items;
        
        if (!string.IsNullOrWhiteSpace(SearchText) || FilterContentType.HasValue || 
            (SelectedGroup != null && SelectedGroup.Id != Guid.Empty))
        {
            items = await _clipboardRepository.SearchAsync(
                SearchText, 
                FilterContentType, 
                SelectedGroup?.Id == Guid.Empty ? null : SelectedGroup?.Id);
        }
        else
        {
            items = await _clipboardRepository.GetAllAsync();
        }
        
        ClipboardItems.Clear();
        foreach (ClipboardItem item in items)
        {
            ClipboardItems.Add(new ClipboardItemViewModel(item));
        }
        
        // Try to restore previously selected item, or select first item
        if (previouslySelectedId.HasValue)
        {
            ClipboardItemViewModel? previousItem = ClipboardItems.FirstOrDefault(i => i.Id == previouslySelectedId.Value);
            SelectedItem = previousItem ?? ClipboardItems.FirstOrDefault();
        }
        else
        {
            SelectedItem = ClipboardItems.FirstOrDefault();
        }
        
        // Update group counts
        await UpdateGroupCountsAsync();
    }
    
    private async Task UpdateGroupCountsAsync()
    {
        // Get total count for "All" group
        int totalCount = await _clipboardRepository.GetCountAsync();
        GroupViewModel? allGroup = Groups.FirstOrDefault(g => g.Id == Guid.Empty);
        if (allGroup != null)
        {
            allGroup.ItemCount = totalCount;
        }
        
        // Get counts by group
        Dictionary<Guid, int> groupCounts = await _clipboardRepository.GetCountsByGroupAsync();
        
        // Update each group's count
        foreach (GroupViewModel group in Groups.Where(g => g.Id != Guid.Empty))
        {
            group.ItemCount = groupCounts.GetValueOrDefault(group.Id, 0);
        }
    }
    
    partial void OnSearchTextChanged(string value)
    {
        _ = SearchAsync();
    }
    
    partial void OnSelectedGroupChanged(GroupViewModel? value)
    {
        // Update selection state
        foreach (GroupViewModel group in Groups)
        {
            group.IsSelected = group.Id == value?.Id;
        }
        
        _ = LoadClipboardItemsAsync();
    }
    
    partial void OnSelectedItemChanged(ClipboardItemViewModel? value)
    {
        // Update selection state in list
        foreach (ClipboardItemViewModel item in ClipboardItems)
        {
            item.IsSelected = item.Id == value?.Id;
        }
        
        // Update detail view
        DetailViewModel.UpdateSelection(value);
    }
    
    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadClipboardItemsAsync();
    }
    
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadClipboardItemsAsync();
    }
    
    [RelayCommand]
    private void SelectGroup(GroupViewModel group)
    {
        SelectedGroup = group;
    }
    
    [RelayCommand]
    private void SelectItem(ClipboardItemViewModel item)
    {
        SelectedItem = item;
    }
    
    /// <summary>
    /// Selects the next item in the clipboard list.
    /// </summary>
    public void SelectNextItem()
    {
        if (ClipboardItems.Count == 0) return;
        
        if (SelectedItem == null)
        {
            SelectedItem = ClipboardItems[0];
            return;
        }
        
        // Find current index by ID (reference equality may fail after list reload)
        int currentIndex = -1;
        for (int i = 0; i < ClipboardItems.Count; i++)
        {
            if (ClipboardItems[i].Id == SelectedItem.Id)
            {
                currentIndex = i;
                break;
            }
        }
        
        if (currentIndex >= 0 && currentIndex < ClipboardItems.Count - 1)
        {
            SelectedItem = ClipboardItems[currentIndex + 1];
        }
        else if (currentIndex < 0 && ClipboardItems.Count > 0)
        {
            // SelectedItem not found in list, select first item
            SelectedItem = ClipboardItems[0];
        }
    }
    
    /// <summary>
    /// Selects the previous item in the clipboard list.
    /// </summary>
    public void SelectPreviousItem()
    {
        if (ClipboardItems.Count == 0) return;
        
        if (SelectedItem == null)
        {
            SelectedItem = ClipboardItems[0];
            return;
        }
        
        // Find current index by ID (reference equality may fail after list reload)
        int currentIndex = -1;
        for (int i = 0; i < ClipboardItems.Count; i++)
        {
            if (ClipboardItems[i].Id == SelectedItem.Id)
            {
                currentIndex = i;
                break;
            }
        }
        
        if (currentIndex > 0)
        {
            SelectedItem = ClipboardItems[currentIndex - 1];
        }
        else if (currentIndex < 0 && ClipboardItems.Count > 0)
        {
            // SelectedItem not found in list, select first item
            SelectedItem = ClipboardItems[0];
        }
    }
    
    /// <summary>
    /// Selects the next group.
    /// </summary>
    public void SelectNextGroup()
    {
        if (Groups.Count == 0) return;
        
        if (SelectedGroup == null)
        {
            SelectedGroup = Groups[0];
            return;
        }
        
        int currentIndex = Groups.IndexOf(SelectedGroup);
        if (currentIndex < Groups.Count - 1)
        {
            SelectedGroup = Groups[currentIndex + 1];
        }
    }
    
    /// <summary>
    /// Selects the previous group.
    /// </summary>
    public void SelectPreviousGroup()
    {
        if (Groups.Count == 0) return;
        
        if (SelectedGroup == null)
        {
            SelectedGroup = Groups[0];
            return;
        }
        
        int currentIndex = Groups.IndexOf(SelectedGroup);
        if (currentIndex > 0)
        {
            SelectedGroup = Groups[currentIndex - 1];
        }
    }
    
    /// <summary>
    /// Copies the currently selected item to the clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopySelectedItemAsync()
    {
        if (SelectedItem == null) return;
        
        try
        {
            await _clipboardService.CopyToClipboardAsync(SelectedItem.Model);
            StatusMessage = "Copied to clipboard";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
            Log.Error(ex, "Failed to copy selected item to clipboard");
        }
    }
    
    [RelayCommand]
    private async Task DeleteItemAsync(ClipboardItemViewModel? item)
    {
        if (item == null) return;
        
        try
        {
            // Use ClipboardService to delete item and associated files
            await _clipboardService.DeleteItemAsync(item.Model);
            ClipboardItems.Remove(item);
            
            if (SelectedItem?.Id == item.Id)
            {
                SelectedItem = ClipboardItems.FirstOrDefault();
            }
            
            await UpdateGroupCountsAsync();
            StatusMessage = "Item deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
            Log.Error(ex, "Failed to delete clipboard item {ItemId}", item.Id);
        }
    }
    
    [RelayCommand]
    private async Task MoveToGroupAsync(GroupViewModel? group)
    {
        // Use SelectedItem as the target for the move operation
        ClipboardItemViewModel? item = SelectedItem;
        if (item == null) return;
        
        try
        {
            Guid? groupId = group?.Id;
            item.Model.GroupId = groupId;
            await _clipboardRepository.UpdateAsync(item.Model);
            
            // Update the view model
            item.GroupId = groupId;
            item.GroupColor = group?.Color;
            
            // Update detail view if this item is selected
            if (SelectedItem?.Id == item.Id)
            {
                DetailViewModel.UpdateSelection(item);
            }
            
            await UpdateGroupCountsAsync();
            
            string groupName = group?.Name ?? "None";
            StatusMessage = $"Item moved to '{groupName}'";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Move failed: {ex.Message}";
            Log.Error(ex, "Failed to move clipboard item {ItemId} to group {GroupId}", item.Id, group?.Id);
        }
    }
    
    public async Task AddGroupAsync(string name, string color)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        
        ClipboardGroup group = new ClipboardGroup
        {
            Name = name.Trim(),
            Color = color
        };
        
        await _groupRepository.AddAsync(group);
        Groups.Add(new GroupViewModel(group));
        
        StatusMessage = $"Group '{name}' created";
    }
    
    [RelayCommand]
    private async Task DeleteGroupAsync(GroupViewModel group)
    {
        if (group.Id == Guid.Empty) return; // Can't delete "All" group
        
        await _groupRepository.DeleteAsync(group.Id);
        Groups.Remove(group);
        
        // If the deleted group was selected, switch to "All"
        if (SelectedGroup?.Id == group.Id)
        {
            SelectedGroup = Groups.FirstOrDefault();
        }
        
        await LoadClipboardItemsAsync();
        StatusMessage = $"Group '{group.Name}' deleted";
    }
    
    [RelayCommand]
    private void FilterByType(ClipboardContentType? contentType)
    {
        FilterContentType = contentType;
        _ = LoadClipboardItemsAsync();
    }
    
    [RelayCommand]
    private async Task ClearAllAsync()
    {
        await _clipboardRepository.DeleteAllAsync();
        ClipboardItems.Clear();
        SelectedItem = null;
        await UpdateGroupCountsAsync();
        StatusMessage = "All items cleared";
    }
    
    /// <summary>
    /// Runs cleanup based on current settings (max items and retention days).
    /// </summary>
    private async Task RunCleanupAsync()
    {
        if (_currentSettings == null) return;
        
        try
        {
            int deleted = await _clipboardService.CleanupAsync(
                _currentSettings.MaxHistoryItems,
                _currentSettings.RetentionDays);
            
            if (deleted > 0)
            {
                Log.Information("Cleanup removed {Count} items (MaxItems={Max}, RetentionDays={Days})",
                    deleted, _currentSettings.MaxHistoryItems, _currentSettings.RetentionDays);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to run cleanup");
        }
    }
    
    /// <summary>
    /// Reloads settings from the database. Call this when settings have changed externally.
    /// </summary>
    public async Task ReloadSettingsAsync()
    {
        try
        {
            _currentSettings = await _settingsRepository.GetSettingsAsync();
            Log.Debug("Settings reloaded: MaxHistoryItems={Max}, RetentionDays={Days}",
                _currentSettings.MaxHistoryItems, _currentSettings.RetentionDays);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload settings");
        }
    }

    private async void OnClipboardItemAdded(object? sender, ClipboardItem item)
    {
        try
        {
            Log.Debug("Clipboard item received: {ItemId}, Type: {ContentType}", item.Id, item.ContentType);
            
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Log.Verbose("Adding item to UI collection");
                
                // Add to top of list
                ClipboardItems.Insert(0, new ClipboardItemViewModel(item));
                
                // Update counts
                await UpdateGroupCountsAsync();
                
                StatusMessage = $"New item captured: {item.ContentType}";
                Log.Debug("Item added to UI, total items: {TotalItems}", ClipboardItems.Count);
                
                // Run cleanup to enforce max items limit
                if (_currentSettings?.MaxHistoryItems > 0)
                {
                    int deleted = await _clipboardService.CleanupAsync(_currentSettings.MaxHistoryItems, 0);
                    if (deleted > 0)
                    {
                        // Reload list to reflect deletions
                        await LoadClipboardItemsAsync();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling clipboard item added event");
            StatusMessage = $"Error adding item: {ex.Message}";
        }
    }
    
    private async void OnCopyRequested(object? sender, ClipboardItem item)
    {
        
        try
        {
            await _clipboardService.CopyToClipboardAsync(item);

            
            StatusMessage = "Copied to clipboard";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }
    
    private async void OnDeleteRequested(object? sender, ClipboardItem item)
    {
        try
        {
            // Use ClipboardService to delete item and associated files
            await _clipboardService.DeleteItemAsync(item);
            
            ClipboardItemViewModel? viewModel = ClipboardItems.FirstOrDefault(vm => vm.Id == item.Id);
            if (viewModel != null)
            {
                ClipboardItems.Remove(viewModel);
            }
            
            if (SelectedItem?.Id == item.Id)
            {
                SelectedItem = ClipboardItems.FirstOrDefault();
            }
            
            await UpdateGroupCountsAsync();
            StatusMessage = "Item deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }
    
    private async void OnGroupChangeRequested(object? sender, (ClipboardItem Item, Guid? GroupId) args)
    {
        try
        {
            args.Item.GroupId = args.GroupId;
            await _clipboardRepository.UpdateAsync(args.Item);
            
            // Refresh the item in the list
            ClipboardItemViewModel? viewModel = ClipboardItems.FirstOrDefault(vm => vm.Id == args.Item.Id);
            if (viewModel != null)
            {
                viewModel.GroupId = args.GroupId;
                GroupViewModel? group = Groups.FirstOrDefault(g => g.Id == args.GroupId);
                viewModel.GroupColor = group?.Color;
            }
            
            await UpdateGroupCountsAsync();
            StatusMessage = "Item moved to group";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Move failed: {ex.Message}";
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        _clipboardService.ItemAdded -= OnClipboardItemAdded;
        DetailViewModel.CopyRequested -= OnCopyRequested;
        DetailViewModel.DeleteRequested -= OnDeleteRequested;
        DetailViewModel.GroupChangeRequested -= OnGroupChangeRequested;
        
        if (_clipboardService is IDisposable disposableService)
        {
            disposableService.Dispose();
        }
        
        _dbContext.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
