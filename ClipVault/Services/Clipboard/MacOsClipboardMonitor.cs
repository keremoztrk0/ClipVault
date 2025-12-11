using ClipVault.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Serilog;

#pragma warning disable CS0618 // Type or member is obsolete (Avalonia clipboard API)

namespace ClipVault.Services.Clipboard;

/// <summary>
/// macOS-specific clipboard monitor using NSPasteboard polling.
/// </summary>
public class MacOsClipboardMonitor : IClipboardMonitor
{
    private bool _isMonitoring;
    private bool _disposed;
    private CancellationTokenSource? _cts;
    private string? _lastContentHash;
    private TopLevel? _topLevel;
    
    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
    
    public bool IsMonitoring => _isMonitoring;
    
    public async Task StartAsync()
    {
        if (_isMonitoring) return;
        
        _isMonitoring = true;
        _cts = new CancellationTokenSource();
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                _topLevel = desktop.MainWindow;
            }
        });
        
        _ = PollClipboardAsync(_cts.Token);
    }
    
    public Task StopAsync()
    {
        _isMonitoring = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }
    
    private async Task PollClipboardAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isMonitoring)
        {
            try
            {
                await Task.Delay(500, cancellationToken);
                
                var content = await GetCurrentContentAsync();
                if (content != null)
                {
                    var hash = content.ComputeHash();
                    if (hash != _lastContentHash)
                    {
                        _lastContentHash = hash;
                        
                        ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
                        {
                            Content = content,
                            SourceApplication = null,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Clipboard polling error on macOS");
            }
        }
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
                
                var formats = await clipboard.GetFormatsAsync();
                
                // Check for files
                if (formats.Contains("public.file-url") || formats.Contains("NSFilenamesPboardType"))
                {
                    var data = await clipboard.GetDataAsync("NSFilenamesPboardType");
                    if (data is IEnumerable<string> files)
                    {
                        var filePaths = files.ToArray();
                        if (filePaths.Length > 0)
                        {
                            return new ClipboardContent
                            {
                                Type = filePaths.Length > 1 ? ClipboardContentType.Files : ClipboardContentType.File,
                                FilePaths = filePaths
                            };
                        }
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
            Log.Warning(ex, "Error getting clipboard content on macOS");
            return null;
        }
    }
    
    public async Task SetContentAsync(ClipboardContent content)
    {
        if (_topLevel == null) return;
        
        try
        {
            var wasMonitoring = _isMonitoring;
            if (wasMonitoring) await StopAsync();
            
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var clipboard = _topLevel.Clipboard;
                if (clipboard == null) return;
                
                if (!string.IsNullOrEmpty(content.Text))
                {
                    await clipboard.SetTextAsync(content.Text);
                }
                else if (content.FilePaths != null && content.FilePaths.Length > 0)
                {
                    await clipboard.SetTextAsync(string.Join(Environment.NewLine, content.FilePaths));
                }
            });
            
            _lastContentHash = content.ComputeHash();
            
            if (wasMonitoring) await StartAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error setting clipboard content on macOS");
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _isMonitoring = false;
        
        GC.SuppressFinalize(this);
    }
}
