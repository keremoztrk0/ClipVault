using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;

namespace ClipVault.App.Data;

/// <summary>
/// Initializes the database schema for ClipVault.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Ensures the database and all tables exist.
    /// </summary>
    public static async Task InitializeAsync(AppDbContext context)
    {
        using SqliteConnection connection = await context.GetOpenConnectionAsync();
        
        await CreateTablesAsync(connection);
        await CreateIndexesAsync(connection);
        await SeedDataAsync(connection);
        
        Log.Information("Database initialized successfully");
    }
    
    private static async Task CreateTablesAsync(SqliteConnection connection)
    {
        // ClipboardGroups table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS ClipboardGroups (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Color TEXT NOT NULL DEFAULT '#3498db',
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            )");
        
        // ClipboardItems table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS ClipboardItems (
                Id TEXT PRIMARY KEY,
                ContentType INTEGER NOT NULL,
                TextContent TEXT,
                FilePath TEXT,
                PreviewText TEXT,
                SourceApplication TEXT,
                GroupId TEXT,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                LastAccessedAt TEXT NOT NULL,
                ContentHash TEXT,
                FOREIGN KEY (GroupId) REFERENCES ClipboardGroups(Id) ON DELETE SET NULL
            )");
        
        // ClipboardMetadata table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS ClipboardMetadata (
                Id TEXT PRIMARY KEY,
                ClipboardItemId TEXT NOT NULL UNIQUE,
                CharacterCount INTEGER,
                WordCount INTEGER,
                LineCount INTEGER,
                FileName TEXT,
                FileSize INTEGER,
                FileExtension TEXT,
                OriginalPath TEXT,
                DurationMs INTEGER,
                Width INTEGER,
                Height INTEGER,
                MimeType TEXT,
                FileCount INTEGER,
                FOREIGN KEY (ClipboardItemId) REFERENCES ClipboardItems(Id) ON DELETE CASCADE
            )");
        
        // AppSettings table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY,
                GlobalHotkey TEXT NOT NULL DEFAULT 'Ctrl+Shift+V',
                Theme TEXT NOT NULL DEFAULT 'Dark',
                WindowOpacity REAL NOT NULL DEFAULT 0.95,
                MaxHistoryItems INTEGER NOT NULL DEFAULT 1000,
                RetentionDays INTEGER NOT NULL DEFAULT 0,
                StartWithSystem INTEGER NOT NULL DEFAULT 0,
                StartMinimized INTEGER NOT NULL DEFAULT 0,
                ShowInTaskbar INTEGER NOT NULL DEFAULT 1
            )");
        
        // Migration: Add new columns if they don't exist
        await MigrateAppSettingsAsync(connection);
    }
    
    private static async Task MigrateAppSettingsAsync(SqliteConnection connection)
    {
        try
        {
            // Check if RetentionDays column exists
            var columns = await connection.QueryAsync<string>(
                "SELECT name FROM pragma_table_info('AppSettings')");
            var columnList = columns.ToList();
            
            if (!columnList.Contains("RetentionDays"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE AppSettings ADD COLUMN RetentionDays INTEGER NOT NULL DEFAULT 0");
                Log.Information("Added RetentionDays column to AppSettings");
            }
            
            if (!columnList.Contains("StartMinimized"))
            {
                await connection.ExecuteAsync(
                    "ALTER TABLE AppSettings ADD COLUMN StartMinimized INTEGER NOT NULL DEFAULT 0");
                Log.Information("Added StartMinimized column to AppSettings");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Migration check failed, columns may already exist");
        }
    }
    
    private static async Task CreateIndexesAsync(SqliteConnection connection)
    {
        // ClipboardItems indexes
        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_CreatedAt ON ClipboardItems(CreatedAt DESC);
            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_ContentType ON ClipboardItems(ContentType);
            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_GroupId ON ClipboardItems(GroupId);
            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_ContentHash ON ClipboardItems(ContentHash);
            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_PreviewText ON ClipboardItems(PreviewText);
        ");
        
        // ClipboardGroups indexes
        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS IX_ClipboardGroups_SortOrder ON ClipboardGroups(SortOrder);
            CREATE INDEX IF NOT EXISTS IX_ClipboardGroups_Name ON ClipboardGroups(Name);
        ");
        
        // ClipboardMetadata indexes
        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS IX_ClipboardMetadata_ClipboardItemId ON ClipboardMetadata(ClipboardItemId);
        ");
    }
    
    private static async Task SeedDataAsync(SqliteConnection connection)
    {
        // Ensure default settings exist
        int settingsExist = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM AppSettings WHERE Id = 1");
        
        if (settingsExist == 0)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO AppSettings (Id, GlobalHotkey, Theme, WindowOpacity, MaxHistoryItems, RetentionDays, StartWithSystem, StartMinimized, ShowInTaskbar)
                VALUES (1, 'Ctrl+Shift+V', 'Dark', 0.95, 1000, 0, 0, 0, 1)");
        }
    }
}
