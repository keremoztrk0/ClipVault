using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using ClipVault.App.Data.Repositories;
using ClipVault.App.ViewModels;
using System.Globalization;

namespace ClipVault.App.Views;

public partial class SettingsWindow : Window
{
    /// <summary>
    /// Converter for displaying max history items (0 = Unlimited).
    /// </summary>
    public static readonly FuncValueConverter<int, string> MaxHistoryConverter =
        new(value => value == 0 ? "Unlimited" : value.ToString("N0"));
    
    /// <summary>
    /// Converter for displaying retention days (0 = Forever).
    /// </summary>
    public static readonly FuncValueConverter<int, string> RetentionDaysConverter =
        new(value => value == 0 ? "Forever" : $"{value} days");
    
    public SettingsWindow()
    {
        InitializeComponent();
    }
    
    public SettingsWindow(ISettingsRepository settingsRepository)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(settingsRepository);
    }
    
    /// <summary>
    /// Initializes and shows the settings window.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.LoadSettingsAsync();
        }
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
    
    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            // Wait for save to complete before closing
            await viewModel.SaveSettingsCommand.ExecuteAsync(null);
        }
        Close(true);
    }
}
