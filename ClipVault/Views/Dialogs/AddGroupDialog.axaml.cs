using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ClipVault.Views.Dialogs;

public partial class AddGroupDialog : Window
{
    private string _selectedColor = "#e74c3c";
    
    public AddGroupDialog()
    {
        InitializeComponent();
    }
    
    private void OnColorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton)
        {
            // Remove selected class from all color buttons
            foreach (var child in ColorPanel.Children)
            {
                if (child is Button button)
                {
                    button.Classes.Remove("selected");
                }
            }
            
            // Add selected class to clicked button
            clickedButton.Classes.Add("selected");
            
            // Store the selected color
            
            if (clickedButton.Tag is string color)
            {
                _selectedColor = color;
            }
        }
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
    
    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim();
        
        if (string.IsNullOrWhiteSpace(name))
        {
            // Show validation error - for now just return
            NameTextBox.Focus();
            return;
        }
        
        Close(new AddGroupResult
        {
            Name = name,
            Color = _selectedColor
        });
    }
    
    public class AddGroupResult
    {
        public required string Name { get; init; }
        public required string Color { get; init; }
    }
}
