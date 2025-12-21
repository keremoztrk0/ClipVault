using ClipVault.App.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ClipVault.App.Data.Repositories;

/// <summary>
/// Repository implementation for application settings using Dapper.
/// </summary>
public class SettingsRepository(IDbConnectionFactory connectionFactory) : ISettingsRepository
{
    public async Task<AppSettings> GetSettingsAsync()
    {
        using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        AppSettings? settings = await connection.QueryFirstOrDefaultAsync<AppSettings>(
            "SELECT * FROM AppSettings WHERE Id = 1");

        if (settings != null) return settings;
        settings = new AppSettings();
        await connection.ExecuteAsync("""

                                                      INSERT INTO AppSettings (Id, GlobalHotkey, Theme, MaxHistoryItems, RetentionDays, StartWithSystem, StartMinimized, ShowInTaskbar)
                                                      VALUES (@Id, @GlobalHotkey, @Theme, @MaxHistoryItems, @RetentionDays, @StartWithSystem, @StartMinimized, @ShowInTaskbar)
                                      """,
            new
            {
                settings.Id,
                settings.GlobalHotkey,
                settings.Theme,
                settings.MaxHistoryItems,
                settings.RetentionDays,
                StartWithSystem = settings.StartWithSystem ? 1 : 0,
                StartMinimized = settings.StartMinimized ? 1 : 0,
                ShowInTaskbar = settings.ShowInTaskbar ? 1 : 0
            });

        return settings;
    }
    
    public async Task<AppSettings> UpdateSettingsAsync(AppSettings settings)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync("""

                                                  UPDATE AppSettings 
                                                  SET GlobalHotkey = @GlobalHotkey, 
                                                      Theme = @Theme, 
                                                      MaxHistoryItems = @MaxHistoryItems, 
                                                      RetentionDays = @RetentionDays,
                                                      StartWithSystem = @StartWithSystem, 
                                                      StartMinimized = @StartMinimized,
                                                      ShowInTaskbar = @ShowInTaskbar
                                                  WHERE Id = @Id
                                      """,
            new
            {
                settings.Id,
                settings.GlobalHotkey,
                settings.Theme,
                settings.MaxHistoryItems,
                settings.RetentionDays,
                StartWithSystem = settings.StartWithSystem ? 1 : 0,
                StartMinimized = settings.StartMinimized ? 1 : 0,
                ShowInTaskbar = settings.ShowInTaskbar ? 1 : 0
            });
        
        return settings;
    }
}
