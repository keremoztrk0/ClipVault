using ClipVault.Models;
using ClipVault.Services.Clipboard;

namespace ClipVault.Services;

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
