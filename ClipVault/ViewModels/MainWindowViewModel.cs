using System.Collections.ObjectModel;
using ClipVault.Data;
using ClipVault.Data.Repositories;
using ClipVault.Models;
using ClipVault.Services;
using ClipVault.Services.Clipboard;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ClipVault.ViewModels;

/// <summary>
/// Main ViewModel that orchestrates the entire application.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IClipboardRepository _clipboardRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IClipboardService _clipboardService;
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
    public ClipboardDetailViewModel DetailViewModel { get; }
    
    public MainWindowViewModel()
    {
        Log.Debug("MainWindowViewModel constructor starting");
        
        // Initialize database context
        _dbContext = new AppDbContext();
        
        // Initialize repositories
        _clipboardRepository = new ClipboardRepository(_dbContext);
        _groupRepository = new GroupRepository(_dbContext);
        
        // Initialize clipboard service
        var monitor = ClipboardMonitorFactory.Create();
        Log.Debug("Created clipboard monitor: {MonitorType}", monitor.GetType().Name);
        
        var metadataExtractor = new MetadataExtractor();
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
        var groups = await _groupRepository.GetAllAsync();
        var totalCount = await _clipboardRepository.GetCountAsync();
        
        Groups.Clear();
        Groups.Add(GroupViewModel.CreateAllGroup(totalCount));
        
        foreach (var group in groups)
        {
            Groups.Add(new GroupViewModel(group));
        }
        
        SelectedGroup = Groups.FirstOrDefault();
    }
    
    private async Task LoadClipboardItemsAsync()
    {
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
        foreach (var item in items)
        {
            ClipboardItems.Add(new ClipboardItemViewModel(item));
        }
        
        // Update group counts
        await UpdateGroupCountsAsync();
    }
    
    private async Task UpdateGroupCountsAsync()
    {
        var totalCount = await _clipboardRepository.GetCountAsync();
        var allGroup = Groups.FirstOrDefault(g => g.Id == Guid.Empty);
        if (allGroup != null)
        {
            allGroup.ItemCount = totalCount;
        }
    }
    
    partial void OnSearchTextChanged(string value)
    {
        _ = SearchAsync();
    }
    
    partial void OnSelectedGroupChanged(GroupViewModel? value)
    {
        // Update selection state
        foreach (var group in Groups)
        {
            group.IsSelected = group.Id == value?.Id;
        }
        
        _ = LoadClipboardItemsAsync();
    }
    
    partial void OnSelectedItemChanged(ClipboardItemViewModel? value)
    {
        // Update selection state in list
        foreach (var item in ClipboardItems)
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
    
    public async Task AddGroupAsync(string name, string color)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        
        var group = new ClipboardGroup
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
            await _clipboardRepository.DeleteAsync(item.Id);
            
            var viewModel = ClipboardItems.FirstOrDefault(vm => vm.Id == item.Id);
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
            var viewModel = ClipboardItems.FirstOrDefault(vm => vm.Id == args.Item.Id);
            if (viewModel != null)
            {
                viewModel.GroupId = args.GroupId;
                var group = Groups.FirstOrDefault(g => g.Id == args.GroupId);
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
