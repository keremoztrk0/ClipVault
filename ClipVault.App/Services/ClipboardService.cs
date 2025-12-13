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
            FilePaths = item.FilePath?.Split(';', StringSplitOptions.RemoveEmptyEntries)
        };

        await _monitor.SetContentAsync(content);
        await _clipboardRepository.UpdateLastAccessedAsync(item.Id);

        Logger.Debug("Copied item to clipboard: {Id}", item.Id);
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