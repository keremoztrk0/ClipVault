using ClipVault.App.Data;
using ClipVault.App.Data.Repositories;
using ClipVault.App.Models;
using ClipVault.App.Services.Clipboard;
using Serilog;

namespace ClipVault.App.Services;

/// <summary>
///     Service that orchestrates clipboard monitoring, content storage, and retrieval.
/// </summary>
public class ClipboardService : IClipboardService, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ClipboardService>();
    private readonly IClipboardRepository _clipboardRepository;
    private readonly IMetadataExtractor _metadataExtractor;

    private readonly IClipboardMonitor _monitor;
    private bool _disposed;

    public ClipboardService(
        IClipboardMonitor monitor,
        IClipboardRepository clipboardRepository,
        IMetadataExtractor metadataExtractor)
    {
        _monitor = monitor;
        _clipboardRepository = clipboardRepository;
        _metadataExtractor = metadataExtractor;

        _monitor.ClipboardChanged += OnClipboardChanged;
        Logger.Debug("ClipboardService initialized");
    }

    public event EventHandler<ClipboardItem>? ItemAdded;

    public bool IsMonitoring => _monitor.IsMonitoring;

    public async Task StartMonitoringAsync()
    {
        Logger.Information("Starting clipboard monitoring");
        await _monitor.StartAsync();
    }

    public async Task StopMonitoringAsync()
    {
        Logger.Information("Stopping clipboard monitoring");
        await _monitor.StopAsync();
    }

    public async Task CopyToClipboardAsync(ClipboardItem item)
    {
        ClipboardContent content = new()
        {
            Type = item.ContentType,
            Text = item.TextContent,
            FilePaths = item.FilePath?.Split(';', StringSplitOptions.RemoveEmptyEntries),
            ImageData = await LoadImageDataAsync(item)
        };

        await _monitor.SetContentAsync(content);
        await _clipboardRepository.UpdateLastAccessedAsync(item.Id);

        Logger.Debug("Copied item to clipboard: {Id}", item.Id);
    }

    public async Task DeleteItemAsync(ClipboardItem item)
    {
        try
        {
            // Delete associated file from storage if it exists and is in our Content folder
            if (!string.IsNullOrEmpty(item.FilePath)) DeleteStoredFile(item.FilePath);

            // Delete the database record
            await _clipboardRepository.DeleteAsync(item.Id);

            Logger.Information("Deleted clipboard item {Id} and associated files", item.Id);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting clipboard item {Id}", item.Id);
            throw;
        }
    }

    public async Task<int> CleanupAsync(int maxItems, int retentionDays)
    {
        int totalDeleted = 0;

        try
        {
            // First, delete items older than retention period
            if (retentionDays > 0)
            {
                DateTime cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                // Get items to delete first so we can clean up their files
                List<ClipboardItem> itemsToDelete = await _clipboardRepository.GetItemsOlderThanAsync(cutoffDate);

                // Delete associated files
                foreach (ClipboardItem item in itemsToDelete)
                    if (!string.IsNullOrEmpty(item.FilePath))
                        DeleteStoredFile(item.FilePath);

                // Now delete from database
                int deletedByAge = await _clipboardRepository.DeleteOlderThanAsync(cutoffDate);
                totalDeleted += deletedByAge;

                if (deletedByAge > 0) Logger.Information("Deleted {Count} items older than {Days} days", deletedByAge, retentionDays);
            }

            // Then, enforce max items limit
            if (maxItems > 0)
            {
                // Get items to delete first so we can clean up their files
                List<ClipboardItem> itemsToDelete = await _clipboardRepository.GetExcessItemsAsync(maxItems);

                // Delete associated files
                foreach (ClipboardItem item in itemsToDelete)
                    if (!string.IsNullOrEmpty(item.FilePath))
                        DeleteStoredFile(item.FilePath);

                // Now delete from database
                int deletedByLimit = await _clipboardRepository.DeleteExcessItemsAsync(maxItems);
                totalDeleted += deletedByLimit;

                if (deletedByLimit > 0) Logger.Information("Deleted {Count} items to enforce max limit of {Max}", deletedByLimit, maxItems);
            }

            if (totalDeleted > 0) Logger.Information("Cleanup completed: {Total} items removed", totalDeleted);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during clipboard cleanup");
        }

        return totalDeleted;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _monitor.ClipboardChanged -= OnClipboardChanged;
        _monitor.Dispose();

        Logger.Debug("ClipboardService disposed");
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Loads image data from the stored file for image content types.
    /// </summary>
    private static async Task<byte[]?> LoadImageDataAsync(ClipboardItem item)
    {
        if (item.ContentType != ClipboardContentType.Image || string.IsNullOrEmpty(item.FilePath))
            return null;

        try
        {
            // Get the first file path (images are stored as single files)
            string filePath = item.FilePath.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

            if (!File.Exists(filePath))
            {
                Logger.Warning("Image file not found: {FilePath}", filePath);
                return null;
            }

            byte[] data = await File.ReadAllBytesAsync(filePath);
            Logger.Debug("Loaded image data from file: {FilePath}, Size: {Size} bytes", filePath, data.Length);
            return data;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load image data from file: {FilePath}", item.FilePath);
        }

        return null;
    }

    /// <summary>
    ///     Deletes a stored content file if it exists within the app's content storage folder.
    /// </summary>
    private static void DeleteStoredFile(string filePath)
    {
        try
        {
            // Only delete files that are stored in our Content folder
            string contentStoragePath = AppDbContext.GetContentStoragePath();

            // Handle multiple file paths (semicolon separated)
            string[] paths = filePath.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (string path in paths)
            {
                string trimmedPath = path.Trim();

                // Security check: only delete files within our content storage folder
                string fullPath = Path.GetFullPath(trimmedPath);
                string storagePath = Path.GetFullPath(contentStoragePath);

                if (fullPath.StartsWith(storagePath, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    Logger.Debug("Deleted stored content file: {FilePath}", fullPath);
                }
                else
                {
                    Logger.Debug("Skipping file deletion (external file): {FilePath}", trimmedPath);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to delete stored file: {FilePath}", filePath);
            // Don't throw - file deletion failure shouldn't prevent item deletion
        }
    }

    private async void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        Logger.Debug("Clipboard changed event received: {Type}", e.Content.Type);
        try
        {
            await ProcessClipboardContentAsync(e);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing clipboard content");
        }
    }

    private async Task ProcessClipboardContentAsync(ClipboardChangedEventArgs e)
    {
        ClipboardContent content = e.Content;
        string? contentHash = content.ComputeHash();

        Logger.Debug("Processing content with hash: {Hash}", contentHash?.Substring(0, 8));

        // Check for duplicate
        ClipboardItem? existing = await _clipboardRepository.GetByHashAsync(contentHash ?? string.Empty);
        if (existing != null)
        {
            Logger.Debug("Duplicate content found, updating last accessed time");
            await _clipboardRepository.UpdateLastAccessedAsync(existing.Id);
            return;
        }

        Logger.Debug("Creating new clipboard item");

        // Create new clipboard item
        ClipboardItem item = new()
        {
            ContentType = content.Type,
            TextContent = content.Text,
            PreviewText = CreatePreviewText(content),
            SourceApplication = e.SourceApplication,
            ContentHash = contentHash,
            CreatedAt = e.Timestamp,
            LastAccessedAt = e.Timestamp
        };

        // Handle file storage for binary content
        if (content is { Type: ClipboardContentType.Image, ImageData: not null })
        {
            string filePath = await SaveContentToFileAsync(content.ImageData, ".png");
            item.FilePath = filePath;
        }
        else if (content.FilePaths is { Length: > 0 })
        {
            item.FilePath = string.Join(";", content.FilePaths);
        }

        // Extract metadata BEFORE saving
        ClipboardMetadata metadata = await _metadataExtractor.ExtractMetadataAsync(content, item.Id);
        item.Metadata = metadata;

        // Save the item with its metadata
        await _clipboardRepository.AddAsync(item);
        Logger.Information("Clipboard item saved: {Id}, Type: {Type}", item.Id, item.ContentType);

        // Notify listeners
        ItemAdded?.Invoke(this, item);
    }

    private static string? CreatePreviewText(ClipboardContent content)
    {
        if (!string.IsNullOrEmpty(content.Text))
        {
            string? preview = content.Text.Length > 500
                ? content.Text[..500] + "..."
                : content.Text;
            return NormalizeWhitespace(preview);
        }

        if (content.FilePaths is { Length: > 0 })
        {
            if (content.FilePaths.Length == 1) return Path.GetFileName(content.FilePaths[0]);
            return $"{content.FilePaths.Length} files";
        }

        return content.Type.ToString();
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(" ", text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
    }

    private static async Task<string> SaveContentToFileAsync(byte[] data, string extension)
    {
        string storagePath = AppDbContext.GetContentStoragePath();
        string fileName = $"{Guid.NewGuid()}{extension}";
        string filePath = Path.Combine(storagePath, fileName);

        await File.WriteAllBytesAsync(filePath, data);
        Logger.Debug("Content saved to file: {FilePath}", filePath);

        return filePath;
    }
}