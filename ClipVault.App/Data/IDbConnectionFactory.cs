using Microsoft.Data.Sqlite;

namespace ClipVault.App.Data;

/// <summary>
/// Interface for database connection factory.
/// </summary>
public interface IDbConnectionFactory : IDisposable
{
    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    SqliteConnection CreateConnection();
    
    /// <summary>
    /// Gets an open database connection.
    /// </summary>
    Task<SqliteConnection> GetOpenConnectionAsync();
}
