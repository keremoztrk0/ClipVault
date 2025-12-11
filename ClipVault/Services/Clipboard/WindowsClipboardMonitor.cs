using System.Runtime.InteropServices;
using ClipVault.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Serilog;

#pragma warning disable CS0618 // Type or member is obsolete (Avalonia clipboard API)

namespace ClipVault.Services.Clipboard;

/// <summary>
/// Windows-specific clipboard monitor using native Win32 clipboard listener API.
/// Uses AddClipboardFormatListener for event-based notifications instead of polling.
/// </summary>
public partial class WindowsClipboardMonitor : IClipboardMonitor
{
    private static readonly ILogger Logger = Log.ForContext<WindowsClipboardMonitor>();
    
    private bool _isMonitoring;
    private bool _disposed;
    private string? _lastContentHash;
    private TopLevel? _topLevel;
    private IntPtr _hwnd;
    private NativeWindowHandler? _nativeHandler;
    private bool _ignoringNextChange;
    
    // Win32 API imports
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();
    
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    
    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
    
    public bool IsMonitoring => _isMonitoring;
    
    public async Task StartAsync()
    {
        if (_isMonitoring) return;
        
        Logger.Information("Starting clipboard monitor (event-based)");
        
        // Get TopLevel from Avalonia - must be done on UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                _topLevel = desktop.MainWindow;
                Logger.Debug("TopLevel acquired: {HasTopLevel}", _topLevel != null);
                
                // Get the native window handle
                if (_topLevel is Window window)
                {
                    var platformHandle = window.TryGetPlatformHandle();
                    if (platformHandle != null)
                    {
                        _hwnd = platformHandle.Handle;
                        Logger.Debug("Window handle acquired: {Handle}", _hwnd);
                    }
                }
            }
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
            _isMonitoring = true;
            Logger.Information("Clipboard listener registered successfully");
            
            // Capture current clipboard state
            await CaptureCurrentClipboardAsync();
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            Logger.Warning("Failed to register clipboard listener (error: {Error}), falling back to polling", error);
            await StartPollingFallbackAsync();
        }
    }
    
    private async Task StartPollingFallbackAsync()
    {
        _isMonitoring = true;
        _ = PollClipboardAsync(CancellationToken.None);
        await Task.CompletedTask;
    }
    
    private async Task PollClipboardAsync(CancellationToken cancellationToken)
    {
        Logger.Debug("Fallback polling started");
        
        while (!cancellationToken.IsCancellationRequested && _isMonitoring)
        {
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
        var content = await GetCurrentContentAsync();
        if (content == null) return;
        
        var hash = content.ComputeHash();
        if (hash == _lastContentHash) return;
        
        Logger.Information("New clipboard content detected: {Type}, Hash: {Hash}", 
            content.Type, hash?.Substring(0, 8));
        _lastContentHash = hash;
        
        var sourceApp = GetForegroundApplicationName();
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
        var content = await GetCurrentContentAsync();
        if (content != null)
        {
            _lastContentHash = content.ComputeHash();
            Logger.Debug("Initial clipboard hash: {Hash}", _lastContentHash?.Substring(0, 8));
        }
    }
    
    public Task StopAsync()
    {
        if (_hwnd != IntPtr.Zero && _isMonitoring)
        {
            RemoveClipboardFormatListener(_hwnd);
            Logger.Information("Clipboard listener removed");
        }
        
        _nativeHandler?.Dispose();
        _nativeHandler = null;
        _isMonitoring = false;
        
        return Task.CompletedTask;
    }
    
    public async Task<ClipboardContent?> GetCurrentContentAsync()
    {
        if (_topLevel == null) return null;
        
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var clipboard = _topLevel.Clipboard;
                if (clipboard == null) return null;
                
                // Check for files first
                var formats = await clipboard.GetFormatsAsync();
                Logger.Debug("Available clipboard formats: {Formats}", string.Join(", ", formats ?? []));
                
                if (formats != null && (formats.Contains("Files") || formats.Contains("FileNames")))
                {
                    var files = await clipboard.GetDataAsync("FileNames") as IEnumerable<string>;
                    if (files != null)
                    {
                        var filePaths = files.ToArray();
                        if (filePaths.Length > 0)
                        {
                            var contentType = DetermineFileContentType(filePaths);
                            Logger.Debug("Clipboard contains files: {Count}, Type: {Type}", filePaths.Length, contentType);
                            return new ClipboardContent
                            {
                                Type = contentType,
                                FilePaths = filePaths
                            };
                        }
                    }
                }
                
                // Check for image/bitmap data (screenshots, copied images)
                if (formats != null && (formats.Contains("PNG") || formats.Contains("Bitmap") || 
                    formats.Contains("image/png") || formats.Contains("image/bmp") ||
                    formats.Contains("DeviceIndependentBitmap")))
                {
                    Logger.Debug("Clipboard contains image data");
                    var imageData = await TryGetImageDataAsync(clipboard, formats);
                    if (imageData != null && imageData.Length > 0)
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
                var text = await clipboard.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    return new ClipboardContent
                    {
                        Type = ClipboardContentType.Text,
                        Text = text
                    };
                }
                
                return null;
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting clipboard content");
            return null;
        }
    }
    
    private async Task<byte[]?> TryGetImageDataAsync(Avalonia.Input.Platform.IClipboard clipboard, string[] formats)
    {
        try
        {
            // Try PNG first (best quality, lossless)
            if (formats.Contains("PNG") || formats.Contains("image/png"))
            {
                var pngData = await clipboard.GetDataAsync("PNG");
                if (pngData is byte[] pngBytes && pngBytes.Length > 0)
                {
                    Logger.Debug("Got PNG data: {Size} bytes", pngBytes.Length);
                    return pngBytes;
                }
                if (pngData is System.IO.Stream pngStream)
                {
                    using var ms = new MemoryStream();
                    await pngStream.CopyToAsync(ms);
                    Logger.Debug("Got PNG stream: {Size} bytes", ms.Length);
                    return ms.ToArray();
                }
            }
            
            // Try DeviceIndependentBitmap (common for screenshots on Windows)
            if (formats.Contains("DeviceIndependentBitmap"))
            {
                var dibData = await clipboard.GetDataAsync("DeviceIndependentBitmap");
                if (dibData is byte[] dibBytes && dibBytes.Length > 0)
                {
                    Logger.Debug("Got DIB data: {Size} bytes", dibBytes.Length);
                    // Convert DIB to PNG for storage
                    return ConvertDibToPng(dibBytes);
                }
                if (dibData is System.IO.Stream dibStream)
                {
                    using var ms = new MemoryStream();
                    await dibStream.CopyToAsync(ms);
                    Logger.Debug("Got DIB stream: {Size} bytes", ms.Length);
                    return ConvertDibToPng(ms.ToArray());
                }
            }
            
            // Try standard Bitmap format
            if (formats.Contains("Bitmap") || formats.Contains("image/bmp"))
            {
                var bmpData = await clipboard.GetDataAsync("Bitmap");
                if (bmpData is byte[] bmpBytes && bmpBytes.Length > 0)
                {
                    Logger.Debug("Got Bitmap data: {Size} bytes", bmpBytes.Length);
                    return bmpBytes;
                }
                if (bmpData is System.IO.Stream bmpStream)
                {
                    using var ms = new MemoryStream();
                    await bmpStream.CopyToAsync(ms);
                    Logger.Debug("Got Bitmap stream: {Size} bytes", ms.Length);
                    return ms.ToArray();
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
            var width = BitConverter.ToInt32(dibData, 4);
            var height = BitConverter.ToInt32(dibData, 8);
            var bitCount = BitConverter.ToInt16(dibData, 14);
            var compression = BitConverter.ToInt32(dibData, 16);
            
            Logger.Debug("DIB: {Width}x{Height}, {BitCount}bpp, compression={Compression}", 
                width, height, bitCount, compression);
            
            // Calculate the size of color table (if any)
            var colorTableSize = 0;
            if (bitCount <= 8)
            {
                var colorsUsed = BitConverter.ToInt32(dibData, 32);
                colorTableSize = (colorsUsed == 0 ? (1 << bitCount) : colorsUsed) * 4;
            }
            
            // Create BMP file header (14 bytes)
            var headerSize = 14;
            var pixelDataOffset = headerSize + 40 + colorTableSize;
            var fileSize = headerSize + dibData.Length;
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            // BMP file header
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(fileSize);
            writer.Write(0); // Reserved
            writer.Write(pixelDataOffset);
            
            // Write the DIB data (BITMAPINFOHEADER + pixel data)
            writer.Write(dibData);
            
            var bmpData = ms.ToArray();
            
            // Now convert BMP to PNG using Avalonia
            using var bmpStream = new MemoryStream(bmpData);
            var bitmap = new Bitmap(bmpStream);
            
            using var pngStream = new MemoryStream();
            bitmap.Save(pngStream);
            
            return pngStream.ToArray();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to convert DIB to PNG, returning raw data");
            return dibData;
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
                var clipboard = _topLevel.Clipboard;
                if (clipboard == null) return;
                
                switch (content.Type)
                {
                    case ClipboardContentType.Text:
                    case ClipboardContentType.RichText:
                    case ClipboardContentType.Html:
                        if (!string.IsNullOrEmpty(content.Text))
                        {
                            await clipboard.SetTextAsync(content.Text);
                        }
                        break;
                        
                    case ClipboardContentType.Image:
                        // For images, copy the file path as text (user can paste in file managers)
                        // Or if we have the original image data, we'd need platform-specific code
                        if (content.FilePaths != null && content.FilePaths.Length > 0)
                        {
                            await clipboard.SetTextAsync(content.FilePaths[0]);
                        }
                        break;
                        
                    case ClipboardContentType.File:
                    case ClipboardContentType.Files:
                        if (content.FilePaths != null && content.FilePaths.Length > 0)
                        {
                            await clipboard.SetTextAsync(string.Join(Environment.NewLine, content.FilePaths));
                        }
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
    
    private static ClipboardContentType DetermineFileContentType(string[] filePaths)
    {
        if (filePaths.Length > 1)
            return ClipboardContentType.Files;
            
        var extension = Path.GetExtension(filePaths[0]).ToLowerInvariant();
        
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
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            
            GetWindowThreadProcessId(hwnd, out int processId);
            var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        if (_hwnd != IntPtr.Zero && _isMonitoring)
        {
            RemoveClipboardFormatListener(_hwnd);
        }
        
        _nativeHandler?.Dispose();
        _isMonitoring = false;
        
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Helper class to handle native window messages via subclassing.
    /// </summary>
    private class NativeWindowHandler : IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<NativeWindowHandler>();
        
        private readonly IntPtr _hwnd;
        private readonly Action _onClipboardUpdate;
        private readonly WndProcDelegate _wndProcDelegate;
        private IntPtr _originalWndProc;
        private bool _disposed;
        
        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        private const int GWLP_WNDPROC = -4;
        
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
        
        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                // Fire on UI thread
                Dispatcher.UIThread.Post(() => _onClipboardUpdate());
            }
            
            return CallWindowProcW(_originalWndProc, hwnd, msg, wParam, lParam);
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            // Restore original window procedure
            if (_originalWndProc != IntPtr.Zero && _hwnd != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _originalWndProc);
                Logger.Debug("Window procedure restored");
            }
        }
    }
}
