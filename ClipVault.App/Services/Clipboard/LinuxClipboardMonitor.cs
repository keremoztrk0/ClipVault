using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using ClipVault.App.Models;
using Serilog;

#pragma warning disable CS0618 // Type or member is obsolete (Avalonia clipboard API)

namespace ClipVault.App.Services.Clipboard;

/// <summary>
/// Linux-specific clipboard monitor using X11/Wayland.
/// Uses polling approach similar to other platforms.
/// </summary>
public class LinuxClipboardMonitor : IClipboardMonitor
{
    private bool _disposed;
    private CancellationTokenSource? _cts;
    private string? _lastContentHash;
    private TopLevel? _topLevel;
    
    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
    
    public bool IsMonitoring { get; private set; }

    public async Task StartAsync()
    {
        if (IsMonitoring) return;
        
        IsMonitoring = true;
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
        IsMonitoring = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }
    
    private async Task PollClipboardAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsMonitoring)
        {
            try
            {
                await Task.Delay(500, cancellationToken);
                
                ClipboardContent? content = await GetCurrentContentAsync();
                if (content == null) continue;
                string hash = content.ComputeHash();
                if (hash == _lastContentHash) continue;
                _lastContentHash = hash;
                        
                ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
                {
                    Content = content,
                    SourceApplication = null,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Clipboard polling error on Linux");
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
                IClipboard? clipboard = _topLevel.Clipboard;
                if (clipboard == null) return null;
                
                string[] formats = await clipboard.GetFormatsAsync();
                
                // Check for files (URI list on Linux)
                if (formats.Contains("text/uri-list"))
                {
                    object? data = await clipboard.GetDataAsync("text/uri-list");
                    if (data is string uriList)
                    {
                        string[] filePaths = uriList
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Select(uri => uri.Trim())
                            .Where(uri => uri.StartsWith("file://"))
                            .Select(uri => new Uri(uri).LocalPath)
                            .ToArray();
                            
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
                string? text = await clipboard.GetTextAsync();
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
            Log.Warning(ex, "Error getting clipboard content on Linux");
            return null;
        }
    }
    
    public async Task SetContentAsync(ClipboardContent content)
    {
        if (_topLevel == null) return;
        
        try
        {
            bool wasMonitoring = IsMonitoring;
            if (wasMonitoring) await StopAsync();
            
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                IClipboard? clipboard = _topLevel.Clipboard;
                if (clipboard == null) return;
                
                if (!string.IsNullOrEmpty(content.Text))
                {
                    await clipboard.SetTextAsync(content.Text);
                }
                else if (content.FilePaths is { Length: > 0 })
                {
                    string uriList = string.Join("\n", content.FilePaths.Select(p => new Uri(p).AbsoluteUri));
                    await clipboard.SetTextAsync(uriList);
                }
            });
            
            _lastContentHash = content.ComputeHash();
            
            if (wasMonitoring) await StartAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error setting clipboard content on Linux");
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
}
