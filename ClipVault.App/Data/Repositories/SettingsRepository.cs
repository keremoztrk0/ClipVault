using ClipVault.App.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ClipVault.App.Data.Repositories;

/// <summary>
/// Repository implementation for application settings using Dapper.
/// </summary>
public class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _context;
    
    public SettingsRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<AppSettings> GetSettingsAsync()
    {
        using SqliteConnection connection = await _context.GetOpenConnectionAsync();
        
        AppSettings? settings = await connection.QueryFirstOrDefaultAsync<AppSettings>(
            "SELECT * FROM AppSettings WHERE Id = 1");
        
        if (settings == null)
        {
            settings = new AppSettings();
            await connection.ExecuteAsync(@"
                INSERT INTO AppSettings (Id, GlobalHotkey, Theme, WindowOpacity, MaxHistoryItems, RetentionDays, StartWithSystem, StartMinimized, ShowInTaskbar)
                VALUES (@Id, @GlobalHotkey, @Theme, @WindowOpacity, @MaxHistoryItems, @RetentionDays, @StartWithSystem, @StartMinimized, @ShowInTaskbar)",
                new
                {
                    settings.Id,
                    settings.GlobalHotkey,
                    settings.Theme,
                    settings.WindowOpacity,
                    settings.MaxHistoryItems,
                    settings.RetentionDays,
                    StartWithSystem = settings.StartWithSystem ? 1 : 0,
                    StartMinimized = settings.StartMinimized ? 1 : 0,
                    ShowInTaskbar = settings.ShowInTaskbar ? 1 : 0
                });
        }
        
        return settings;
    }
    
    public async Task<AppSettings> UpdateSettingsAsync(AppSettings settings)
    {
        using SqliteConnection connection = await _context.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync(@"
            UPDATE AppSettings 
            SET GlobalHotkey = @GlobalHotkey, 
                Theme = @Theme, 
                WindowOpacity = @WindowOpacity,
                MaxHistoryItems = @MaxHistoryItems, 
                RetentionDays = @RetentionDays,
                StartWithSystem = @StartWithSystem, 
                StartMinimized = @StartMinimized,
                ShowInTaskbar = @ShowInTaskbar
            WHERE Id = @Id",
            new
            {
                settings.Id,
                settings.GlobalHotkey,
                settings.Theme,
                settings.WindowOpacity,
                settings.MaxHistoryItems,
                settings.RetentionDays,
                StartWithSystem = settings.StartWithSystem ? 1 : 0,
                StartMinimized = settings.StartMinimized ? 1 : 0,
                ShowInTaskbar = settings.ShowInTaskbar ? 1 : 0
            });
        
        return settings;
    }
}
