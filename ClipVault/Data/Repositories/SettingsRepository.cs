using ClipVault.Models;
using Dapper;

namespace ClipVault.Data.Repositories;

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
        using var connection = await _context.GetOpenConnectionAsync();
        
        var settings = await connection.QueryFirstOrDefaultAsync<AppSettings>(
            "SELECT * FROM AppSettings WHERE Id = 1");
        
        if (settings == null)
        {
            settings = new AppSettings();
            await connection.ExecuteAsync(@"
                INSERT INTO AppSettings (Id, GlobalHotkey, Theme, WindowOpacity, MaxHistoryItems, StartWithSystem, ShowInTaskbar)
                VALUES (@Id, @GlobalHotkey, @Theme, @WindowOpacity, @MaxHistoryItems, @StartWithSystem, @ShowInTaskbar)",
                new
                {
                    settings.Id,
                    settings.GlobalHotkey,
                    settings.Theme,
                    settings.WindowOpacity,
                    settings.MaxHistoryItems,
                    StartWithSystem = settings.StartWithSystem ? 1 : 0,
                    ShowInTaskbar = settings.ShowInTaskbar ? 1 : 0
                });
        }
        
        return settings;
    }
    
    public async Task<AppSettings> UpdateSettingsAsync(AppSettings settings)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync(@"
            UPDATE AppSettings 
            SET GlobalHotkey = @GlobalHotkey, Theme = @Theme, WindowOpacity = @WindowOpacity,
                MaxHistoryItems = @MaxHistoryItems, StartWithSystem = @StartWithSystem, ShowInTaskbar = @ShowInTaskbar
            WHERE Id = @Id",
            new
            {
                settings.Id,
                settings.GlobalHotkey,
                settings.Theme,
                settings.WindowOpacity,
                settings.MaxHistoryItems,
                StartWithSystem = settings.StartWithSystem ? 1 : 0,
                ShowInTaskbar = settings.ShowInTaskbar ? 1 : 0
            });
        
        return settings;
    }
}
