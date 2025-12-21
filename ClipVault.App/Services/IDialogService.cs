using ClipVault.App.Views.Dialogs;

namespace ClipVault.App.Services;

/// <summary>
/// Service interface for showing dialogs.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The dialog message.</param>
    /// <param name="confirmText">The confirm button text.</param>
    /// <returns>True if the user confirmed, false otherwise.</returns>
    Task<bool> ShowConfirmDialogAsync(string title, string message, string confirmText = "OK");
    
    /// <summary>
    /// Shows the add group dialog.
    /// </summary>
    /// <returns>The result containing group name and color, or null if cancelled.</returns>
    Task<AddGroupDialog.AddGroupResult?> ShowAddGroupDialogAsync();
    
    /// <summary>
    /// Shows the settings dialog.
    /// </summary>
    /// <returns>True if settings were saved, false if cancelled.</returns>
    Task<bool> ShowSettingsDialogAsync();
    
    /// <summary>
    /// Gets or sets whether a dialog is currently open.
    /// </summary>
    bool IsDialogOpen { get; }
}
