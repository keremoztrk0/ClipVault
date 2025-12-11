using ClipVault.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipVault.ViewModels;

/// <summary>
/// ViewModel for the detail view (right panel).
/// </summary>
public partial class ClipboardDetailViewModel : ViewModelBase
{
    [ObservableProperty]
    private ClipboardItemViewModel? _selectedItem;
    
    [ObservableProperty]
    private string? _contentDisplay;
    
    [ObservableProperty]
    private string? _contentTypeDisplay;
    
    [ObservableProperty]
    private string? _characterCountDisplay;
    
    [ObservableProperty]
    private string? _wordCountDisplay;
    
    [ObservableProperty]
    private string? _lineCountDisplay;
    
    [ObservableProperty]
    private string? _fileSizeDisplay;
    
    [ObservableProperty]
    private string? _fileNameDisplay;
    
    [ObservableProperty]
    private string? _dimensionsDisplay;
    
    [ObservableProperty]
    private string? _durationDisplay;
    
    [ObservableProperty]
    private string? _sourceAppDisplay;
    
    [ObservableProperty]
    private string? _createdAtDisplay;
    
    [ObservableProperty]
    private string? _mimeTypeDisplay;
    
    [ObservableProperty]
    private GroupViewModel? _selectedGroup;
    
    [ObservableProperty]
    private bool _hasSelection;
    
    [ObservableProperty]
    private bool _isTextContent;
    
    [ObservableProperty]
    private bool _isImageContent;
    
    [ObservableProperty]
    private bool _isFileContent;
    
    public event EventHandler<ClipboardItem>? CopyRequested;
    public event EventHandler<ClipboardItem>? DeleteRequested;
    public event EventHandler<(ClipboardItem Item, Guid? GroupId)>? GroupChangeRequested;
    
    public void UpdateSelection(ClipboardItemViewModel? item)
    {
        SelectedItem = item;
        HasSelection = item != null;
        
        if (item == null)
        {
            ClearDisplay();
            return;
        }
        
        var model = item.Model;
        
        // Set content type flags
        IsTextContent = model.ContentType is ClipboardContentType.Text 
            or ClipboardContentType.RichText 
            or ClipboardContentType.Html;
        IsImageContent = model.ContentType == ClipboardContentType.Image;
        IsFileContent = model.ContentType is ClipboardContentType.File 
            or ClipboardContentType.Files 
            or ClipboardContentType.Video 
            or ClipboardContentType.Audio;
        
        // Content display
        if (IsTextContent)
        {
            ContentDisplay = model.TextContent;
        }
        else if (IsImageContent && !string.IsNullOrEmpty(model.FilePath))
        {
            ContentDisplay = model.FilePath;
        }
        else if (IsFileContent)
        {
            ContentDisplay = model.Metadata?.OriginalPath ?? model.FilePath;
        }
        
        // Type display
        ContentTypeDisplay = model.ContentType.ToString();
        
        // Metadata displays
        var metadata = model.Metadata;
        if (metadata != null)
        {
            CharacterCountDisplay = metadata.CharacterCount?.ToString("N0");
            WordCountDisplay = metadata.WordCount?.ToString("N0");
            LineCountDisplay = metadata.LineCount?.ToString("N0");
            FileSizeDisplay = FormatFileSize(metadata.FileSize);
            FileNameDisplay = metadata.FileName;
            MimeTypeDisplay = metadata.MimeType;
            
            if (metadata.Width.HasValue && metadata.Height.HasValue)
            {
                DimensionsDisplay = $"{metadata.Width} x {metadata.Height}";
            }
            else
            {
                DimensionsDisplay = null;
            }
            
            if (metadata.DurationMs.HasValue)
            {
                DurationDisplay = FormatDuration(metadata.DurationMs.Value);
            }
            else
            {
                DurationDisplay = null;
            }
        }
        else
        {
            ClearMetadataDisplays();
        }
        
        // Source and time
        SourceAppDisplay = model.SourceApplication ?? "Unknown";
        CreatedAtDisplay = model.CreatedAt.ToLocalTime().ToString("g");
    }
    
    private void ClearDisplay()
    {
        ContentDisplay = null;
        ContentTypeDisplay = null;
        IsTextContent = false;
        IsImageContent = false;
        IsFileContent = false;
        ClearMetadataDisplays();
        SourceAppDisplay = null;
        CreatedAtDisplay = null;
    }
    
    private void ClearMetadataDisplays()
    {
        CharacterCountDisplay = null;
        WordCountDisplay = null;
        LineCountDisplay = null;
        FileSizeDisplay = null;
        FileNameDisplay = null;
        DimensionsDisplay = null;
        DurationDisplay = null;
        MimeTypeDisplay = null;
    }
    
    private static string? FormatFileSize(long? bytes)
    {
        if (!bytes.HasValue) return null;
        
        var b = bytes.Value;
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double size = b;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {sizes[order]}";
    }
    
    private static string FormatDuration(long milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(milliseconds);
        
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
        
        return $"{span.Minutes}:{span.Seconds:D2}";
    }
    
    [RelayCommand]
    private void CopyToClipboard()
    {
        if (SelectedItem?.Model != null)
        {
            CopyRequested?.Invoke(this, SelectedItem.Model);
        }
    }
    
    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem?.Model != null)
        {
            DeleteRequested?.Invoke(this, SelectedItem.Model);
        }
    }
    
    [RelayCommand]
    private void ChangeGroup(Guid? groupId)
    {
        if (SelectedItem?.Model != null)
        {
            GroupChangeRequested?.Invoke(this, (SelectedItem.Model, groupId));
        }
    }
}
