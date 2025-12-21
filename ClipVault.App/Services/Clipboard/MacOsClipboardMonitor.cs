using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClipVault.App.Models;
using Microsoft.Extensions.Logging;
using Serilog;

#pragma warning disable CS0618 // Type or member is obsolete (Avalonia clipboard API)

namespace ClipVault.App.Services.Clipboard;

/// <summary>
///     macOS-specific clipboard monitor using native NSPasteboard API.
///     Uses changeCount for efficient change detection instead of content hashing.
/// </summary>
public partial class MacOsClipboardMonitor(ILogger<MacOsClipboardMonitor> logger) : IClipboardMonitor
{
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private long _lastChangeCount = -1;
    private TopLevel? _topLevel;

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public bool IsMonitoring { get; private set; }

    public async Task StartAsync()
    {
        if (IsMonitoring) return;

        logger.LogInformation("Starting clipboard monitor (native NSPasteboard)");

        IsMonitoring = true;
        _cts = new CancellationTokenSource();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                _topLevel = desktop.MainWindow;
        });

        // Get initial change count
        _lastChangeCount = NativeMethods.GetPasteboardChangeCount();
        logger.LogDebug("Initial pasteboard changeCount: {ChangeCount}", _lastChangeCount);

        _ = PollClipboardAsync(_cts.Token);
    }

    public Task StopAsync()
    {
        logger.LogInformation("Stopping clipboard monitor");
        IsMonitoring = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    public async Task<ClipboardContent?> GetCurrentContentAsync()
    {
        if (_topLevel == null) return null;

        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                IClipboard? clipboard = _topLevel.Clipboard;
                if (clipboard == null) return null;

                string[]? formats = await clipboard.GetFormatsAsync();
                logger.LogDebug("Available clipboard formats: {Formats}", string.Join(", ", formats ?? []));

                // Check for files first
                if (formats != null && (formats.Contains("public.file-url") || formats.Contains("NSFilenamesPboardType")))
                {
                    // Try to get file URLs from native pasteboard
                    string[]? filePaths = NativeMethods.GetFilePathsFromPasteboard();
                    if (filePaths is { Length: > 0 })
                    {
                        ClipboardContentType contentType = DetermineFileContentType(filePaths);
                        logger.LogDebug("Clipboard contains files: {Count}, Type: {Type}", filePaths.Length, contentType);
                        return new ClipboardContent
                        {
                            Type = contentType,
                            FilePaths = filePaths
                        };
                    }
                }

                // Check for image data
                if (formats != null && (formats.Contains("public.png") || formats.Contains("public.tiff") ||
                                        formats.Contains("public.jpeg") || formats.Contains("NSPasteboardTypePNG") ||
                                        formats.Contains("NSPasteboardTypeTIFF")))
                {
                    logger.LogDebug("Clipboard contains image data");
                    byte[]? imageData = NativeMethods.GetImageDataFromPasteboard();
                    if (imageData is { Length: > 0 })
                    {
                        logger.LogInformation("Retrieved image from clipboard: {Size} bytes", imageData.Length);
                        return new ClipboardContent
                        {
                            Type = ClipboardContentType.Image,
                            ImageData = imageData
                        };
                    }
                }

                // Check for text
                string? text = await clipboard.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                    return new ClipboardContent
                    {
                        Type = ClipboardContentType.Text,
                        Text = text
                    };

                return null;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting clipboard content");
            return null;
        }
    }

    public async Task SetContentAsync(ClipboardContent content)
    {
        if (_topLevel == null) return;

        try
        {
            // Capture change count before setting (we'll ignore changes up to and including ours)
            long preChangeCount = NativeMethods.GetPasteboardChangeCount();

            // Handle image content using Avalonia's DataObject
            if (content is { Type: ClipboardContentType.Image, ImageData.Length: > 0 })
            {
                bool success = await SetImageToClipboardAsync(content.ImageData);
                if (success)
                {
                    _lastChangeCount = NativeMethods.GetPasteboardChangeCount();
                    logger.LogDebug("Set image to pasteboard via Avalonia, changeCount: {ChangeCount}", _lastChangeCount);
                    return;
                }
                logger.LogWarning("Failed to set image via Avalonia, falling back to text");
            }

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                IClipboard? clipboard = _topLevel.Clipboard;
                if (clipboard == null) return;

                switch (content.Type)
                {
                    case ClipboardContentType.Text:
                    case ClipboardContentType.RichText:
                    case ClipboardContentType.Html:
                        if (!string.IsNullOrEmpty(content.Text))
                            await clipboard.SetTextAsync(content.Text);
                        break;

                    case ClipboardContentType.Image:
                        // Fallback: if Avalonia failed and we have a file path, set it as text
                        if (content.FilePaths is { Length: > 0 })
                            await clipboard.SetTextAsync(content.FilePaths[0]);
                        break;

                    case ClipboardContentType.File:
                    case ClipboardContentType.Files:
                        if (content.FilePaths is { Length: > 0 })
                            await clipboard.SetTextAsync(string.Join(Environment.NewLine, content.FilePaths));
                        break;
                }
            });

            // Update change count to skip our own change
            _lastChangeCount = NativeMethods.GetPasteboardChangeCount();
            logger.LogDebug("Updated lastChangeCount after set: {ChangeCount}", _lastChangeCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting clipboard content");
        }
    }
    
    private async Task<bool> SetImageToClipboardAsync(byte[] imageData)
    {
        if (_topLevel == null) return false;
        
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                IClipboard? clipboard = _topLevel.Clipboard;
                if (clipboard == null) return false;
                
                // Create a DataObject with the image data in multiple formats
                var dataObject = new DataObject();
                
                // Set raw PNG data for various format names (macOS uses UTI types)
                dataObject.Set("public.png", imageData);
                dataObject.Set("PNG", imageData);
                dataObject.Set("image/png", imageData);
                
                // Load the PNG into a Bitmap and convert for TIFF format (common on macOS)
                using MemoryStream pngStream = new(imageData);
                Bitmap bitmap = new(pngStream);
                
                using MemoryStream bmpStream = new();
                bitmap.Save(bmpStream);
                byte[] bmpData = bmpStream.ToArray();
                
                // Set as generic bitmap data
                dataObject.Set("public.tiff", bmpData);
                
                await clipboard.SetDataObjectAsync(dataObject);
                
                logger.LogDebug("Image set to clipboard via Avalonia DataObject: {Size} bytes", imageData.Length);
                return true;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set image to clipboard");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        IsMonitoring = false;

        GC.SuppressFinalize(this);
    }

    private async Task PollClipboardAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Clipboard polling started with native changeCount detection");

        while (!cancellationToken.IsCancellationRequested && IsMonitoring)
        {
            try
            {
                await Task.Delay(250, cancellationToken); // Faster polling since changeCount check is cheap

                // Check if changeCount has changed (very lightweight native call)
                long currentChangeCount = NativeMethods.GetPasteboardChangeCount();
                if (currentChangeCount == _lastChangeCount) continue;

                logger.LogDebug("Pasteboard changeCount changed: {Old} -> {New}", _lastChangeCount, currentChangeCount);
                _lastChangeCount = currentChangeCount;

                // Only fetch content when we detect a change
                ClipboardContent? content = await GetCurrentContentAsync();
                if (content == null) continue;

                string? sourceApp = NativeMethods.GetFrontmostApplicationName();
                logger.LogInformation("New clipboard content detected: {Type}, Source: {Source}",
                    content.Type, sourceApp ?? "unknown");

                ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
                {
                    Content = content,
                    SourceApplication = sourceApp,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Clipboard polling error");
            }
        }

        logger.LogDebug("Clipboard polling stopped");
    }

    private static ClipboardContentType DetermineFileContentType(string[] filePaths)
    {
        if (filePaths.Length > 1)
            return ClipboardContentType.Files;

        string extension = Path.GetExtension(filePaths[0]).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".heic" => ClipboardContentType.Image,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm" or ".m4v" => ClipboardContentType.Video,
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".aiff" => ClipboardContentType.Audio,
            _ => ClipboardContentType.File
        };
    }

    /// <summary>
    ///     Native macOS interop methods using Objective-C runtime.
    /// </summary>
    private static partial class NativeMethods
    {
        private const string FoundationLib = "/System/Library/Frameworks/Foundation.framework/Foundation";
        private const string AppKitLib = "/System/Library/Frameworks/AppKit.framework/AppKit";
        private const string ObjCLib = "/usr/lib/libobjc.dylib";

        // Objective-C runtime
        [LibraryImport(ObjCLib, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr objc_getClass(string className);

        [LibraryImport(ObjCLib, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr sel_registerName(string selectorName);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial long objc_msgSend_long(IntPtr receiver, IntPtr selector);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, long arg1);

        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);
        
        [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static partial IntPtr objc_msgSend_ptr_long(IntPtr receiver, IntPtr selector, IntPtr arg1, long arg2);

        // Foundation helpers
        [LibraryImport(FoundationLib)]
        private static partial IntPtr NSStringFromClass(IntPtr cls);

        // Cached selectors and classes
        private static readonly IntPtr NSPasteboardClass = objc_getClass("NSPasteboard");
        private static readonly IntPtr NSWorkspaceClass = objc_getClass("NSWorkspace");
        private static readonly IntPtr NSArrayClass = objc_getClass("NSArray");
        private static readonly IntPtr NSStringClass = objc_getClass("NSString");
        private static readonly IntPtr NSURLClass = objc_getClass("NSURL");

        private static readonly IntPtr GeneralPasteboardSel = sel_registerName("generalPasteboard");
        private static readonly IntPtr ChangeCountSel = sel_registerName("changeCount");
        private static readonly IntPtr TypesSel = sel_registerName("types");
        private static readonly IntPtr CountSel = sel_registerName("count");
        private static readonly IntPtr ObjectAtIndexSel = sel_registerName("objectAtIndex:");
        private static readonly IntPtr UTF8StringSel = sel_registerName("UTF8String");
        private static readonly IntPtr DataForTypeSel = sel_registerName("dataForType:");
        private static readonly IntPtr BytesSel = sel_registerName("bytes");
        private static readonly IntPtr LengthSel = sel_registerName("length");
        private static readonly IntPtr SharedWorkspaceSel = sel_registerName("sharedWorkspace");
        private static readonly IntPtr FrontmostApplicationSel = sel_registerName("frontmostApplication");
        private static readonly IntPtr LocalizedNameSel = sel_registerName("localizedName");
        private static readonly IntPtr ReadObjectsForClassesSel = sel_registerName("readObjectsForClasses:options:");
        private static readonly IntPtr PathSel = sel_registerName("path");
        private static readonly IntPtr StringWithUtf8StringSel = sel_registerName("stringWithUTF8String:");
        private static readonly IntPtr ArrayWithObjectSel = sel_registerName("arrayWithObject:");

        /// <summary>
        ///     Gets the current pasteboard change count. This is incremented each time
        ///     the pasteboard contents change, making it very efficient for polling.
        /// </summary>
        public static long GetPasteboardChangeCount()
        {
            try
            {
                IntPtr pasteboard = objc_msgSend(NSPasteboardClass, GeneralPasteboardSel);
                if (pasteboard == IntPtr.Zero) return -1;

                return objc_msgSend_long(pasteboard, ChangeCountSel);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get pasteboard change count");
                return -1;
            }
        }

        /// <summary>
        ///     Gets the name of the frontmost application.
        /// </summary>
        public static string? GetFrontmostApplicationName()
        {
            try
            {
                IntPtr workspace = objc_msgSend(NSWorkspaceClass, SharedWorkspaceSel);
                if (workspace == IntPtr.Zero) return null;

                IntPtr frontmostApp = objc_msgSend(workspace, FrontmostApplicationSel);
                if (frontmostApp == IntPtr.Zero) return null;

                IntPtr localizedName = objc_msgSend(frontmostApp, LocalizedNameSel);
                if (localizedName == IntPtr.Zero) return null;

                IntPtr utf8Ptr = objc_msgSend(localizedName, UTF8StringSel);
                if (utf8Ptr == IntPtr.Zero) return null;

                return Marshal.PtrToStringUTF8(utf8Ptr);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get frontmost application name");
                return null;
            }
        }

        /// <summary>
        ///     Gets file paths from the pasteboard using NSURL.
        /// </summary>
        public static string[]? GetFilePathsFromPasteboard()
        {
            try
            {
                IntPtr pasteboard = objc_msgSend(NSPasteboardClass, GeneralPasteboardSel);
                if (pasteboard == IntPtr.Zero) return null;

                // Create array with NSURL class
                IntPtr urlClassArray = objc_msgSend(NSArrayClass, ArrayWithObjectSel, NSURLClass);
                if (urlClassArray == IntPtr.Zero) return null;

                // Read URLs from pasteboard
                IntPtr urls = objc_msgSend(pasteboard, ReadObjectsForClassesSel, urlClassArray, IntPtr.Zero);
                if (urls == IntPtr.Zero) return null;

                long count = objc_msgSend_long(urls, CountSel);
                if (count == 0) return null;

                List<string> paths = new();
                for (long i = 0; i < count; i++)
                {
                    IntPtr url = objc_msgSend(urls, ObjectAtIndexSel, i);
                    if (url == IntPtr.Zero) continue;

                    IntPtr path = objc_msgSend(url, PathSel);
                    if (path == IntPtr.Zero) continue;

                    IntPtr utf8Ptr = objc_msgSend(path, UTF8StringSel);
                    if (utf8Ptr == IntPtr.Zero) continue;

                    string? pathStr = Marshal.PtrToStringUTF8(utf8Ptr);
                    if (!string.IsNullOrEmpty(pathStr))
                        paths.Add(pathStr);
                }

                return paths.Count > 0 ? paths.ToArray() : null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get file paths from pasteboard");
                return null;
            }
        }

        /// <summary>
        ///     Gets image data (PNG) from the pasteboard.
        /// </summary>
        public static byte[]? GetImageDataFromPasteboard()
        {
            try
            {
                IntPtr pasteboard = objc_msgSend(NSPasteboardClass, GeneralPasteboardSel);
                if (pasteboard == IntPtr.Zero) return null;

                // Try PNG first, then TIFF
                string[] imageTypes = ["public.png", "public.tiff", "public.jpeg"];

                foreach (string imageType in imageTypes)
                {
                    IntPtr typeString = CreateNSString(imageType);
                    if (typeString == IntPtr.Zero) continue;

                    IntPtr data = objc_msgSend(pasteboard, DataForTypeSel, typeString);
                    if (data == IntPtr.Zero) continue;

                    long length = objc_msgSend_long(data, LengthSel);
                    if (length <= 0) continue;

                    IntPtr bytes = objc_msgSend(data, BytesSel);
                    if (bytes == IntPtr.Zero) continue;

                    byte[] imageData = new byte[length];
                    Marshal.Copy(bytes, imageData, 0, (int)length);

                    Log.Debug("Retrieved {Type} image data: {Size} bytes", imageType, length);
                    return imageData;
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get image data from pasteboard");
                return null;
            }
        }

        private static IntPtr CreateNSString(string str)
        {
            IntPtr utf8Ptr = Marshal.StringToCoTaskMemUTF8(str);
            try
            {
                return objc_msgSend(NSStringClass, StringWithUtf8StringSel, utf8Ptr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8Ptr);
            }
        }
    }
}
