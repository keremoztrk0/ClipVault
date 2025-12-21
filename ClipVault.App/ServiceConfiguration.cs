using ClipVault.App.Data;
using ClipVault.App.Data.Repositories;
using ClipVault.App.Helpers;
using ClipVault.App.Services;
using ClipVault.App.Services.Clipboard;
using ClipVault.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ClipVault.App;

/// <summary>
/// Configures the dependency injection container for the application.
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Configures and builds the service provider.
    /// </summary>
    public static IServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();
        
        // Logging - integrates Serilog with Microsoft.Extensions.Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });
        
        // Data layer
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();
        
        // Repositories
        services.AddSingleton<IClipboardRepository, ClipboardRepository>();
        services.AddSingleton<IGroupRepository, GroupRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        
        // Services
        services.AddSingleton<IClipboardMonitor>(sp => ClipboardMonitorFactory.Create(sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IMetadataExtractor, MetadataExtractor>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<IWindowSettingsService, WindowSettingsService>();
        services.AddSingleton<TrayIconManager>();
        services.AddSingleton<ITrayIconService>(sp => sp.GetRequiredService<TrayIconManager>());
        
        // Helpers (HotkeyManager needs to be singleton for global hotkey registration)
        services.AddSingleton<HotkeyManager>();
        
        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ClipboardDetailViewModel>();
        services.AddTransient<SettingsViewModel>();
        
        return services.BuildServiceProvider();
    }
}
