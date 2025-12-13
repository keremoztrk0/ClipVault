using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ClipVault.App.Models;
using Serilog;

#pragma warning disable CS0618 // Type or member is obsolete (Avalonia clipboard API)

namespace ClipVault.App.Services.Clipboard;

/// <summary>
///     Windows-specific clipboard monitor using native Win32 clipboard listener API.
///     Uses AddClipboardFormatListener for event-based notifications instead of polling.
/// </summary>
public partial class WindowsClipboardMonitor : IClipboardMonitor
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly ILogger Logger = Log.ForContext<WindowsClipboardMonitor>();

    private bool _disposed;
    private IntPtr _hwnd;
    private bool _ignoringNextChange;
    private string? _lastContentHash;
    private NativeWindowHandler? _nativeHandler;
    private TopLevel? _topLevel;

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public bool IsMonitoring { get; private set; }

    public async Task StartAsync()
    {
        if (IsMonitoring) return;

        Logger.Information("Starting clipboard monitor (event-based)");

        // Get TopLevel from Avalonia - must be done on UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            _topLevel = desktop.MainWindow;
            Logger.Debug("TopLevel acquired: {HasTopLevel}", _topLevel != null);

            // Get the native window handle
            if (_topLevel is not Window window) return;
            IPlatformHandle? platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null) return;
            _hwnd = platformHandle.Handle;
            Logger.Debug("Window handle acquired: {Handle}", _hwnd);
        });

        if (_hwnd == IntPtr.Zero)
        {
            Logger.Warning("Could not get window handle, falling back to polling");
            await StartPollingFallbackAsync();
            return;
        }

        // Create native window handler for receiving messages
        _nativeHandler = new NativeWindowHandler(_hwnd, OnClipboardUpdate);

        // Register for clipboard notifications
        if (AddClipboardFormatListener(_hwnd))
        {
            IsMonitoring = true;
            Logger.Information("Clipboard listener registered successfully");

            // Capture current clipboard state
            await CaptureCurrentClipboardAsync();
        }
        else
        {
            int error = Marshal.GetLastWin32Error();
            Logger.Warning("Failed to register clipboard listener (error: {Error}), falling back to polling", error);
            await StartPollingFallbackAsync();
        }
    }

    public Task StopAsync()
    {
        if (_hwnd != IntPtr.Zero && IsMonitoring)
        {
            RemoveClipboardFormatListener(_hwnd);
            Logger.Information("Clipboard listener removed");
        }

        _nativeHandler?.Dispose();
        _nativeHandler = null;
        IsMonitoring = false;

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

                // Check for files first
                string[]? formats = await clipboard.GetFormatsAsync();
                Logger.Debug("Available clipboard formats: {Formats}", string.Join(", ", formats ?? []));

                if (formats != null && (formats.Contains("Files") || formats.Contains("FileNames")))
                    if (await clipboard.GetDataAsync("FileNames") is IEnumerable<string> files)
                    {
                        string[] filePaths = files.ToArray();
                        if (filePaths.Length > 0)
                        {
                            ClipboardContentType contentType = DetermineFileContentType(filePaths);
                            Logger.Debug("Clipboard contains files: {Count}, Type: {Type}", filePaths.Length, contentType);
                            return new ClipboardContent
                            {
                                Type = contentType,
                                FilePaths = filePaths
                            };
                        }
                    }

                // Check for image/bitmap data (screenshots, copied images)
                if (formats != null && (formats.Contains("PNG") || formats.Contains("Bitmap") ||
                                        formats.Contains("image/png") || formats.Contains("image/bmp") ||
                                        formats.Contains("DeviceIndependentBitmap")))
                {
                    Logger.Debug("Clipboard contains image data");
                    byte[]? imageData = await TryGetImageDataAsync(clipboard, formats);
                    if (imageData is { Length: > 0 })
                    {
                        Logger.Information("Retrieved image from clipboard: {Size} bytes", imageData.Length);
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
            Logger.Error(ex, "Error getting clipboard content");
            return null;
        }
    }

    public async Task SetContentAsync(ClipboardContent content)
    {
        if (_topLevel == null) return;

        try
        {
            // Set flag to ignore the next clipboard change (self-triggered)
            _ignoringNextChange = true;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                IClipboard? clipboard = _topLevel.Clipboard;
                if (clipboard == null) return;

                switch (content.Type)
                {
                    case ClipboardContentType.Text:
                    case ClipboardContentType.RichText:
                    case ClipboardContentType.Html:
                        if (!string.IsNullOrEmpty(content.Text)) await clipboard.SetTextAsync(content.Text);
                        break;

                    case ClipboardContentType.Image:
                        // For images, copy the file path as text (user can paste in file managers)
                        // Or if we have the original image data, we'd need platform-specific code
                        if (content.FilePaths != null && content.FilePaths.Length > 0) await clipboard.SetTextAsync(content.FilePaths[0]);
                        break;

                    case ClipboardContentType.File:
                    case ClipboardContentType.Files:
                        if (content.FilePaths != null && content.FilePaths.Length > 0) await clipboard.SetTextAsync(string.Join(Environment.NewLine, content.FilePaths));
                        break;
                }
            });

            // Update hash to prevent duplicate detection
            _lastContentHash = content.ComputeHash();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error setting clipboard content");
        }
        finally
        {
            // Reset flag after a short delay to ensure clipboard event has fired
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                _ignoringNextChange = false;
            });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        if (_hwnd != IntPtr.Zero && IsMonitoring) RemoveClipboardFormatListener(_hwnd);

        _nativeHandler?.Dispose();
        IsMonitoring = false;

        GC.SuppressFinalize(this);
    }

    // Win32 API imports
    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AddClipboardFormatListener(IntPtr hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveClipboardFormatListener(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetClipboardOwner();

    private async Task StartPollingFallbackAsync()
    {
        IsMonitoring = true;
        _ = PollClipboardAsync(CancellationToken.None);
        await Task.CompletedTask;
    }

    private async Task PollClipboardAsync(CancellationToken cancellationToken)
    {
        Logger.Debug("Fallback polling started");

        while (!cancellationToken.IsCancellationRequested && IsMonitoring)
            try
            {
                await Task.Delay(500, cancellationToken);
                await CheckClipboardAndNotifyAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Polling error");
            }
    }

    private async void OnClipboardUpdate()
    {
        if (_ignoringNextChange)
        {
            _ignoringNextChange = false;
            Logger.Debug("Ignoring self-triggered clipboard change");
            return;
        }

        Logger.Debug("WM_CLIPBOARDUPDATE received");

        try
        {
            await CheckClipboardAndNotifyAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling clipboard update");
        }
    }

    private async Task CheckClipboardAndNotifyAsync()
    {
        ClipboardContent? content = await GetCurrentContentAsync();
        if (content == null) return;

        string? hash = content.ComputeHash();
        if (hash == _lastContentHash) return;

        Logger.Information("New clipboard content detected: {Type}, Hash: {Hash}",
            content.Type, hash?.Substring(0, 8));
        _lastContentHash = hash;

        string? sourceApp = GetForegroundApplicationName();
        Logger.Debug("Source application: {SourceApp}", sourceApp);

        ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
        {
            Content = content,
            SourceApplication = sourceApp,
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task CaptureCurrentClipboardAsync()
    {
        ClipboardContent? content = await GetCurrentContentAsync();
        if (content != null)
        {
            _lastContentHash = content.ComputeHash();
            Logger.Debug("Initial clipboard hash: {Hash}", _lastContentHash?.Substring(0, 8));
        }
    }

    private static async Task<byte[]?> TryGetImageDataAsync(IClipboard clipboard, string[] formats)
    {
        try
        {
            // Try PNG first (best quality, lossless)
            if (formats.Contains("PNG") || formats.Contains("image/png"))
            {
                object? pngData = await clipboard.GetDataAsync("PNG");
                switch (pngData)
                {
                    case byte[] { Length: > 0 } pngBytes:
                        Logger.Debug("Got PNG data: {Size} bytes", pngBytes.Length);
                        return pngBytes;
                    case Stream pngStream:
                    {
                        using MemoryStream ms = new();
                        await pngStream.CopyToAsync(ms);
                        Logger.Debug("Got PNG stream: {Size} bytes", ms.Length);
                        return ms.ToArray();
                    }
                }
            }

            // Try DeviceIndependentBitmap (common for screenshots on Windows)
            if (formats.Contains("DeviceIndependentBitmap"))
            {
                object? dibData = await clipboard.GetDataAsync("DeviceIndependentBitmap");
                switch (dibData)
                {
                    case byte[] { Length: > 0 } dibBytes:
                        Logger.Debug("Got DIB data: {Size} bytes", dibBytes.Length);
                        // Convert DIB to PNG for storage
                        return ConvertDibToPng(dibBytes);
                    case Stream dibStream:
                    {
                        using MemoryStream ms = new();
                        await dibStream.CopyToAsync(ms);
                        Logger.Debug("Got DIB stream: {Size} bytes", ms.Length);
                        return ConvertDibToPng(ms.ToArray());
                    }
                }
            }

            // Try standard Bitmap format
            if (formats.Contains("Bitmap") || formats.Contains("image/bmp"))
            {
                object? bmpData = await clipboard.GetDataAsync("Bitmap");
                switch (bmpData)
                {
                    case byte[] { Length: > 0 } bmpBytes:
                        Logger.Debug("Got Bitmap data: {Size} bytes", bmpBytes.Length);
                        return bmpBytes;
                    case Stream bmpStream:
                    {
                        using MemoryStream ms = new();
                        await bmpStream.CopyToAsync(ms);
                        Logger.Debug("Got Bitmap stream: {Size} bytes", ms.Length);
                        return ms.ToArray();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to get image data from clipboard");
        }

        return null;
    }

    private static byte[]? ConvertDibToPng(byte[] dibData)
    {
        try
        {
            // DIB format: BITMAPINFOHEADER followed by pixel data
            // We need to add a BMP file header to make it a valid BMP, then convert to PNG

            if (dibData.Length < 40) return null; // BITMAPINFOHEADER is 40 bytes

            // Read BITMAPINFOHEADER
            int width = BitConverter.ToInt32(dibData, 4);
            int height = BitConverter.ToInt32(dibData, 8);
            short bitCount = BitConverter.ToInt16(dibData, 14);
            int compression = BitConverter.ToInt32(dibData, 16);

            Logger.Debug("DIB: {Width}x{Height}, {BitCount}bpp, compression={Compression}",
                width, height, bitCount, compression);

            // Calculate the size of color table (if any)
            int colorTableSize = 0;
            if (bitCount <= 8)
            {
                int colorsUsed = BitConverter.ToInt32(dibData, 32);
                colorTableSize = (colorsUsed == 0 ? 1 << bitCount : colorsUsed) * 4;
            }

            // Create BMP file header (14 bytes)
            const int headerSize = 14;
            int pixelDataOffset = headerSize + 40 + colorTableSize;
            int fileSize = headerSize + dibData.Length;

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            // BMP file header
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(fileSize);
            writer.Write(0); // Reserved
            writer.Write(pixelDataOffset);

            // Write the DIB data (BITMAPINFOHEADER + pixel data)
            writer.Write(dibData);

            byte[] bmpData = ms.ToArray();

            // Now convert BMP to PNG using Avalonia
            using MemoryStream bmpStream = new(bmpData);
            Bitmap bitmap = new(bmpStream);

            using MemoryStream pngStream = new();
            bitmap.Save(pngStream);

            return pngStream.ToArray();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to convert DIB to PNG, returning raw data");
            return dibData;
        }
    }

    private static ClipboardContentType DetermineFileContentType(string[] filePaths)
    {
        if (filePaths.Length > 1)
            return ClipboardContentType.Files;

        string extension = Path.GetExtension(filePaths[0]).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" => ClipboardContentType.Image,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm" => ClipboardContentType.Video,
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" => ClipboardContentType.Audio,
            _ => ClipboardContentType.File
        };
    }

    private static string? GetForegroundApplicationName()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out int processId);
            Process process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Helper class to handle native window messages via subclassing.
    /// </summary>
    private partial class NativeWindowHandler : IDisposable
    {
        private const int GWLP_WNDPROC = -4;
        private static readonly ILogger Logger = Log.ForContext<NativeWindowHandler>();

        private readonly IntPtr _hwnd;
        private readonly Action _onClipboardUpdate;
        private readonly IntPtr _originalWndProc;
        private readonly WndProcDelegate _wndProcDelegate;
        private bool _disposed;

        public NativeWindowHandler(IntPtr hwnd, Action onClipboardUpdate)
        {
            _hwnd = hwnd;
            _onClipboardUpdate = onClipboardUpdate;

            // Keep delegate alive
            _wndProcDelegate = WndProc;

            // Subclass the window
            _originalWndProc = GetWindowLongPtr(_hwnd, GWLP_WNDPROC);
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

            Logger.Debug("Window subclassed for clipboard messages");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Restore original window procedure
            if (_originalWndProc == IntPtr.Zero || _hwnd == IntPtr.Zero) return;
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _originalWndProc);
            Logger.Debug("Window procedure restored");
        }

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static partial IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static partial IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static partial IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [LibraryImport("user32.dll")]
        private static partial IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : GetWindowLong32(hWnd, nIndex);
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_CLIPBOARDUPDATE)
                // Fire on UI thread
                Dispatcher.UIThread.Post(() => _onClipboardUpdate());

            return CallWindowProcW(_originalWndProc, hwnd, msg, wParam, lParam);
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}