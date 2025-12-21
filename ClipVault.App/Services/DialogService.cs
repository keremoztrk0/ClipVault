using Avalonia.Controls;
using ClipVault.App.Helpers;
using ClipVault.App.Views;
using ClipVault.App.Views.Dialogs;
using Microsoft.Extensions.Logging;

namespace ClipVault.App.Services;

/// <summary>
/// Service implementation for showing dialogs.
/// </summary>
public class DialogService(ILogger<DialogService> logger, HotkeyManager hotkeyManager) : IDialogService
{
    private Window? _ownerWindow;
    
    /// <inheritdoc/>
    public bool IsDialogOpen { get; private set; }

    /// <summary>
    /// Sets the owner window for dialogs.
    /// </summary>
    /// <param name="window">The owner window.</param>
    public void SetOwnerWindow(Window window)
    {
        _ownerWindow = window;
    }
    
    /// <inheritdoc/>
    public async Task<bool> ShowConfirmDialogAsync(string title, string message, string confirmText = "OK")
    {
        if (_ownerWindow == null)
        {
            logger.LogWarning("Cannot show dialog: owner window not set");
            return false;
        }
        
        IsDialogOpen = true;
        try
        {
            ConfirmDialog dialog = new(title, message, confirmText);
            return await dialog.ShowDialog<bool>(_ownerWindow);
        }
        finally
        {
            IsDialogOpen = false;
        }
    }
    
    /// <inheritdoc/>
    public async Task<AddGroupDialog.AddGroupResult?> ShowAddGroupDialogAsync()
    {
        if (_ownerWindow == null)
        {
            logger.LogWarning("Cannot show dialog: owner window not set");
            return null;
        }
        
        IsDialogOpen = true;
        try
        {
            AddGroupDialog dialog = new();
            return await dialog.ShowDialog<AddGroupDialog.AddGroupResult?>(_ownerWindow);
        }
        finally
        {
            IsDialogOpen = false;
        }
    }
    
    /// <inheritdoc/>
    public async Task<bool> ShowSettingsDialogAsync()
    {
        if (_ownerWindow == null)
        {
            logger.LogWarning("Cannot show dialog: owner window not set");
            return false;
        }
        
        IsDialogOpen = true;
        try
        {
            logger.LogDebug("Opening settings window...");
            SettingsWindow settingsWindow = new(hotkeyManager);
            
            logger.LogDebug("SettingsWindow created, initializing...");
            await settingsWindow.InitializeAsync();
            
            logger.LogDebug("SettingsWindow initialized, showing dialog...");
            bool? result = await settingsWindow.ShowDialog<bool?>(_ownerWindow);
            
            return result == true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open settings window");
            return false;
        }
        finally
        {
            IsDialogOpen = false;
        }
    }
}
