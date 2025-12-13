using ClipVault.App.Models;
using ClipVault.App.Services.Clipboard;

namespace ClipVault.App.Services;

/// <summary>
/// Interface for extracting metadata from clipboard content.
/// </summary>
public interface IMetadataExtractor
{
    /// <summary>
    /// Extracts metadata from clipboard content.
    /// </summary>
    Task<ClipboardMetadata> ExtractMetadataAsync(ClipboardContent content, Guid clipboardItemId);
}
