using ClipVault.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipVault.ViewModels;

/// <summary>
/// ViewModel for a clipboard group chip.
/// </summary>
public partial class GroupViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _color = "#3498db";
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private int _itemCount;
    
    public ClipboardGroup? Model { get; }
    
    /// <summary>
    /// Creates a GroupViewModel from a ClipboardGroup model.
    /// </summary>
    public GroupViewModel(ClipboardGroup model)
    {
        Model = model;
        Id = model.Id;
        Name = model.Name;
        Color = model.Color;
        ItemCount = model.Items?.Count ?? 0;
    }
    
    /// <summary>
    /// Creates the special "All" group view model.
    /// </summary>
    public static GroupViewModel CreateAllGroup(int totalCount)
    {
        return new GroupViewModel
        {
            Id = Guid.Empty,
            Name = "All",
            Color = "#6c757d",
            IsSelected = true,
            ItemCount = totalCount
        };
    }
    
    private GroupViewModel() { }
}
