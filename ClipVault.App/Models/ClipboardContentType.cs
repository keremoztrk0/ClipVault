namespace ClipVault.App.Models;

/// <summary>
/// Represents the type of content stored in a clipboard item.
/// </summary>
public enum ClipboardContentType
{
    Text = 0,
    Image = 1,
    File = 2,
    Files = 3,  // Multiple files
    Video = 4,
    Audio = 5,
    RichText = 6,
    Html = 7,
    Unknown = 99
}
