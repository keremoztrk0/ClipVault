using ClipVault.App.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipVault.App.ViewModels;

/// <summary>
/// ViewModel for a single clipboard item in the list.
/// </summary>
public partial class ClipboardItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;
    
    [ObservableProperty]
    private ClipboardContentType _contentType;
    
    [ObservableProperty]
    private string? _previewText;
    
    [ObservableProperty]
    private string? _sourceApplication;
    
    [ObservableProperty]
    private DateTime _createdAt;
    
    [ObservableProperty]
    private bool _isFavorite;
    
    [ObservableProperty]
    private Guid? _groupId;
    
    [ObservableProperty]
    private string? _groupColor;
    
    [ObservableProperty]
    private bool _isSelected;
    
    // Original model reference for full data access
    public ClipboardItem Model { get; }
    
    public ClipboardItemViewModel(ClipboardItem model)
    {
        Model = model;
        Id = model.Id;
        ContentType = model.ContentType;
        PreviewText = model.PreviewText ?? GetDefaultPreview(model);
        SourceApplication = model.SourceApplication;
        CreatedAt = model.CreatedAt;
        IsFavorite = model.IsFavorite;
        GroupId = model.GroupId;
        GroupColor = model.Group?.Color;
    }
    
    private static string GetDefaultPreview(ClipboardItem model)
    {
        return model.ContentType switch
        {
            ClipboardContentType.Text => model.TextContent?.Length > 100 
                ? model.TextContent[..100] + "..." 
                : model.TextContent ?? "Text",
            ClipboardContentType.Image => "Image",
            ClipboardContentType.File => model.Metadata?.FileName ?? "File",
            ClipboardContentType.Files => $"{model.Metadata?.FileCount ?? 0} files",
            ClipboardContentType.Video => model.Metadata?.FileName ?? "Video",
            ClipboardContentType.Audio => model.Metadata?.FileName ?? "Audio",
            _ => model.ContentType.ToString()
        };
    }
    
    public string ContentTypeIcon => ContentType switch
    {
        ClipboardContentType.Text => "M3,5H9V11H3V5M5,7V9H7V7H5M11,7H21V9H11V7M11,15H21V17H11V15M5,20L1.5,16.5L2.91,15.09L5,17.17L9.59,12.59L11,14L5,20Z",
        ClipboardContentType.Image => "M19,19H5V5H19M19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M13.96,12.29L11.21,15.83L9.25,13.47L6.5,17H17.5L13.96,12.29Z",
        ClipboardContentType.File => "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z",
        ClipboardContentType.Files => "M15,7H20.5L15,1.5V7M8,0H16L22,6V18A2,2 0 0,1 20,20H8C6.89,20 6,19.1 6,18V2A2,2 0 0,1 8,0M4,4V22H20V24H4A2,2 0 0,1 2,22V4H4Z",
        ClipboardContentType.Video => "M17,10.5V7A1,1 0 0,0 16,6H4A1,1 0 0,0 3,7V17A1,1 0 0,0 4,18H16A1,1 0 0,0 17,17V13.5L21,17.5V6.5L17,10.5Z",
        ClipboardContentType.Audio => "M14,3.23V5.29C16.89,6.15 19,8.83 19,12C19,15.17 16.89,17.84 14,18.7V20.77C18,19.86 21,16.28 21,12C21,7.72 18,4.14 14,3.23M16.5,12C16.5,10.23 15.5,8.71 14,7.97V16C15.5,15.29 16.5,13.76 16.5,12M3,9V15H7L12,20V4L7,9H3Z",
        ClipboardContentType.RichText => "M5,3C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3H5M5,5H19V19H5V5M7,7V9H17V7H7M7,11V13H17V11H7M7,15V17H14V15H7Z",
        ClipboardContentType.Html => "M12,17.56L16.07,16.43L16.62,10.33H9.38L9.2,8.3H16.8L17,6.31H7L7.56,12.32H14.45L14.22,14.9L12,15.5L9.78,14.9L9.64,13.24H7.64L7.93,16.43L12,17.56M4.07,3H19.93L18.5,19.2L12,21L5.5,19.2L4.07,3Z",
        _ => "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z"
    };
    
    public string TimeAgo => GetTimeAgo(CreatedAt);
    
    private static string GetTimeAgo(DateTime dateTime)
    {
        TimeSpan span = DateTime.UtcNow - dateTime;
        
        if (span.TotalMinutes < 1)
            return "Just now";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays}d ago";
        
        return dateTime.ToLocalTime().ToString("MMM d");
    }
}
