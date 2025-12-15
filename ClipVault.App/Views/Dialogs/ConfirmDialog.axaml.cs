using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ClipVault.App.Views.Dialogs;

/// <summary>
/// A reusable confirmation dialog for delete operations.
/// </summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Creates a confirmation dialog with custom title, message, and button text.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message.</param>
    /// <param name="confirmButtonText">The text for the confirm button.</param>
    public ConfirmDialog(string title, string message, string confirmButtonText = "Delete") : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmButtonText;
        Title = title;
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
    
    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
