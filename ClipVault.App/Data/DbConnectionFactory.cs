using Microsoft.Data.Sqlite;

namespace ClipVault.App.Data;

/// <summary>
/// Database connection factory for ClipVault using SQLite.
/// </summary>
public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private bool _disposed;
    
    public DbConnectionFactory()
    {
        string dbPath = GetDatabasePath();
        _connectionString = $"Data Source={dbPath}";
    }
    
    /// <inheritdoc/>
    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
    
    /// <inheritdoc/>
    public async Task<SqliteConnection> GetOpenConnectionAsync()
    {
        SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        return connection;
    }
    
    private static string GetDatabasePath()
    {
        string appDataPath = GetAppDataPath();
        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, "clipvault.db");
    }
    
    /// <summary>
    /// Gets the platform-specific application data path.
    /// </summary>
    public static string GetAppDataPath()
    {
        string basePath;
        
        if (OperatingSystem.IsWindows())
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else // Linux and others
        {
            string? xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            basePath = !string.IsNullOrEmpty(xdgConfig) 
                ? xdgConfig 
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }
        
        return Path.Combine(basePath, "ClipVault");
    }
    
    /// <summary>
    /// Gets the path where clipboard content files are stored.
    /// </summary>
    public static string GetContentStoragePath()
    {
        string path = Path.Combine(GetAppDataPath(), "Content");
        Directory.CreateDirectory(path);
        return path;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
