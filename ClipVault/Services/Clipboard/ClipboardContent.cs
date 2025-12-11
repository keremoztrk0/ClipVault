using ClipVault.Models;

namespace ClipVault.Services.Clipboard;

/// <summary>
/// Represents content retrieved from the system clipboard.
/// </summary>
public class ClipboardContent
{
    public ClipboardContentType Type { get; init; }
    public string? Text { get; init; }
    public byte[]? ImageData { get; init; }
    public string[]? FilePaths { get; init; }
    public string? HtmlContent { get; init; }
    public string? RtfContent { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Computes a hash of the content for duplicate detection.
    /// </summary>
    public string ComputeHash()
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hashInput;
        
        if (ImageData != null)
        {
            hashInput = ImageData;
        }
        else if (FilePaths != null && FilePaths.Length > 0)
        {
            hashInput = System.Text.Encoding.UTF8.GetBytes(string.Join("|", FilePaths));
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            hashInput = System.Text.Encoding.UTF8.GetBytes(Text);
        }
        else
        {
            hashInput = Array.Empty<byte>();
        }
        
        var hash = sha256.ComputeHash(hashInput);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Event arguments for clipboard change events.
/// </summary>
public class ClipboardChangedEventArgs : EventArgs
{
    public required ClipboardContent Content { get; init; }
    public string? SourceApplication { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
