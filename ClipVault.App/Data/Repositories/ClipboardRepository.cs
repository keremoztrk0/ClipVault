using ClipVault.App.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ClipVault.App.Data.Repositories;

/// <summary>
/// Repository implementation for clipboard items using Dapper.
/// </summary>
public class ClipboardRepository(IDbConnectionFactory connectionFactory) : IClipboardRepository
{
    public async Task<ClipboardItem?> GetByIdAsync(Guid id)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        ClipboardItem? item = await connection.QueryFirstOrDefaultAsync<ClipboardItem>(
            "SELECT * FROM ClipboardItems WHERE Id = @Id",
            new { Id = id.ToString() });

        if (item == null) return item;
        item.Metadata = await connection.QueryFirstOrDefaultAsync<ClipboardMetadata>(
            "SELECT * FROM ClipboardMetadata WHERE ClipboardItemId = @ClipboardItemId",
            new { ClipboardItemId = id.ToString() });
            
        if (item.GroupId.HasValue)
        {
            item.Group = await connection.QueryFirstOrDefaultAsync<ClipboardGroup>(
                "SELECT * FROM ClipboardGroups WHERE Id = @Id",
                new { Id = item.GroupId.Value.ToString() });
        }

        return item;
    }
    
    public async Task<ClipboardItem?> GetByHashAsync(string contentHash)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        ClipboardItem? item = await connection.QueryFirstOrDefaultAsync<ClipboardItem>(
            "SELECT * FROM ClipboardItems WHERE ContentHash = @ContentHash",
            new { ContentHash = contentHash });
        
        if (item != null)
        {
            item.Metadata = await connection.QueryFirstOrDefaultAsync<ClipboardMetadata>(
                "SELECT * FROM ClipboardMetadata WHERE ClipboardItemId = @ClipboardItemId",
                new { ClipboardItemId = item.Id.ToString() });
        }
        
        return item;
    }
    
    public async Task<List<ClipboardItem>> GetAllAsync(int? limit = null, int offset = 0)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        string sql = "SELECT * FROM ClipboardItems ORDER BY CreatedAt DESC";
        
        if (limit.HasValue)
        {
            sql += " LIMIT @Limit OFFSET @Offset";
        }
        else if (offset > 0)
        {
            sql += " LIMIT -1 OFFSET @Offset";
        }
        
        List<ClipboardItem> items = (await connection.QueryAsync<ClipboardItem>(sql, new { Limit = limit, Offset = offset })).ToList();
        
        await LoadRelatedDataAsync(connection, items);
        
        return items;
    }
    
    public async Task<List<ClipboardItem>> GetByGroupAsync(Guid groupId, int? limit = null, int offset = 0)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        string sql = "SELECT * FROM ClipboardItems WHERE GroupId = @GroupId ORDER BY CreatedAt DESC";
        
        if (limit.HasValue)
        {
            sql += " LIMIT @Limit OFFSET @Offset";
        }
        
        List<ClipboardItem> items = (await connection.QueryAsync<ClipboardItem>(sql, 
            new { GroupId = groupId.ToString(), Limit = limit, Offset = offset })).ToList();
        
        await LoadRelatedDataAsync(connection, items);
        
        return items;
    }
    
    public async Task<List<ClipboardItem>> SearchAsync(
        string? searchTerm, 
        ClipboardContentType? contentType = null, 
        Guid? groupId = null)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        string sql = "SELECT * FROM ClipboardItems WHERE 1=1";
        DynamicParameters parameters = new DynamicParameters();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql += """
                    AND (
                                   PreviewText LIKE @SearchTerm 
                                   OR TextContent LIKE @SearchTerm 
                                   OR SourceApplication LIKE @SearchTerm)
                   """;
            parameters.Add("SearchTerm", $"%{searchTerm}%");
        }
        
        if (contentType.HasValue)
        {
            sql += " AND ContentType = @ContentType";
            parameters.Add("ContentType", (int)contentType.Value);
        }
        
        if (groupId.HasValue)
        {
            sql += " AND GroupId = @GroupId";
            parameters.Add("GroupId", groupId.Value.ToString());
        }
        
        sql += " ORDER BY CreatedAt DESC";
        
        List<ClipboardItem> items = (await connection.QueryAsync<ClipboardItem>(sql, parameters)).ToList();
        
        await LoadRelatedDataAsync(connection, items);
        
        return items;
    }
    
    public async Task<ClipboardItem> AddAsync(ClipboardItem item)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        await using SqliteTransaction transaction = connection.BeginTransaction();
        
        try
        {
            await connection.ExecuteAsync(@"
                INSERT INTO ClipboardItems (Id, ContentType, TextContent, FilePath, PreviewText, SourceApplication, GroupId, IsFavorite, CreatedAt, LastAccessedAt, ContentHash)
                VALUES (@Id, @ContentType, @TextContent, @FilePath, @PreviewText, @SourceApplication, @GroupId, @IsFavorite, @CreatedAt, @LastAccessedAt, @ContentHash)",
                new
                {
                    Id = item.Id.ToString(),
                    ContentType = (int)item.ContentType,
                    item.TextContent,
                    item.FilePath,
                    item.PreviewText,
                    item.SourceApplication,
                    GroupId = item.GroupId?.ToString(),
                    IsFavorite = item.IsFavorite ? 1 : 0,
                    CreatedAt = item.CreatedAt.ToString("O"),
                    LastAccessedAt = item.LastAccessedAt.ToString("O"),
                    item.ContentHash
                }, transaction);
            
            if (item.Metadata != null)
            {
                item.Metadata.ClipboardItemId = item.Id;
                await connection.ExecuteAsync("""

                                                                  INSERT INTO ClipboardMetadata (Id, ClipboardItemId, CharacterCount, WordCount, LineCount, FileName, FileSize, FileExtension, OriginalPath, DurationMs, Width, Height, MimeType, FileCount)
                                                                  VALUES (@Id, @ClipboardItemId, @CharacterCount, @WordCount, @LineCount, @FileName, @FileSize, @FileExtension, @OriginalPath, @DurationMs, @Width, @Height, @MimeType, @FileCount)
                                              """,
                    new
                    {
                        Id = item.Metadata.Id.ToString(),
                        ClipboardItemId = item.Metadata.ClipboardItemId.ToString(),
                        item.Metadata.CharacterCount,
                        item.Metadata.WordCount,
                        item.Metadata.LineCount,
                        item.Metadata.FileName,
                        item.Metadata.FileSize,
                        item.Metadata.FileExtension,
                        item.Metadata.OriginalPath,
                        item.Metadata.DurationMs,
                        item.Metadata.Width,
                        item.Metadata.Height,
                        item.Metadata.MimeType,
                        item.Metadata.FileCount
                    }, transaction);
            }
            
            transaction.Commit();
            return item;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    
    public async Task<ClipboardItem> UpdateAsync(ClipboardItem item)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync("""

                                                  UPDATE ClipboardItems 
                                                  SET ContentType = @ContentType, TextContent = @TextContent, FilePath = @FilePath, 
                                                      PreviewText = @PreviewText, SourceApplication = @SourceApplication, GroupId = @GroupId,
                                                      IsFavorite = @IsFavorite, LastAccessedAt = @LastAccessedAt, ContentHash = @ContentHash
                                                  WHERE Id = @Id
                                      """,
            new
            {
                Id = item.Id.ToString(),
                ContentType = (int)item.ContentType,
                item.TextContent,
                item.FilePath,
                item.PreviewText,
                item.SourceApplication,
                GroupId = item.GroupId?.ToString(),
                IsFavorite = item.IsFavorite ? 1 : 0,
                LastAccessedAt = item.LastAccessedAt.ToString("O"),
                item.ContentHash
            });
        
        return item;
    }
    
    public async Task DeleteAsync(Guid id)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        // Metadata will be deleted by CASCADE
        await connection.ExecuteAsync(
            "DELETE FROM ClipboardItems WHERE Id = @Id",
            new { Id = id.ToString() });
    }
    
    public async Task DeleteAllAsync()
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync("DELETE FROM ClipboardMetadata");
        await connection.ExecuteAsync("DELETE FROM ClipboardItems");
    }
    
    public async Task<int> GetCountAsync()
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ClipboardItems");
    }
    
    public async Task<int> GetCountByGroupAsync(Guid groupId)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ClipboardItems WHERE GroupId = @GroupId",
            new { GroupId = groupId.ToString() });
    }
    
    public async Task<Dictionary<Guid, int>> GetCountsByGroupAsync()
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        IEnumerable<dynamic> results = await connection.QueryAsync(
            "SELECT GroupId, COUNT(*) as Count FROM ClipboardItems WHERE GroupId IS NOT NULL GROUP BY GroupId");
        
        Dictionary<Guid, int> counts = new();
        foreach (dynamic row in results)
        {
            if (row.GroupId is not string groupIdStr) continue;
            Guid gid = Guid.Parse(groupIdStr);
            counts[gid] = (int)(long)row.Count;
        }
        
        return counts;
    }
    
    public async Task UpdateLastAccessedAsync(Guid id)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync(
            "UPDATE ClipboardItems SET LastAccessedAt = @LastAccessedAt WHERE Id = @Id",
            new { Id = id.ToString(), LastAccessedAt = DateTime.UtcNow.ToString("O") });
    }
    
    public async Task<List<ClipboardItem>> GetItemsOlderThanAsync(DateTime olderThan)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        // Get non-favorite items older than the specified date that are NOT in a group
        List<ClipboardItem> items = (await connection.QueryAsync<ClipboardItem>(
            """
            SELECT * FROM ClipboardItems 
                          WHERE CreatedAt < @OlderThan AND IsFavorite = 0 AND GroupId IS NULL
            """,
            new { OlderThan = olderThan.ToString("O") })).ToList();
        
        return items;
    }
    
    public async Task<List<ClipboardItem>> GetExcessItemsAsync(int maxItems)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        // Get total count of items that can be deleted (not in a group)
        int totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ClipboardItems WHERE GroupId IS NULL");
        
        if (totalCount <= maxItems)
        {
            return [];
        }
        
        int toDelete = totalCount - maxItems;
        
        // Get oldest non-favorite items that are NOT in a group
        List<ClipboardItem> items = (await connection.QueryAsync<ClipboardItem>(
            """
            SELECT * FROM ClipboardItems 
                          WHERE IsFavorite = 0 AND GroupId IS NULL
                          ORDER BY CreatedAt ASC 
                          LIMIT @ToDelete
            """,
            new { ToDelete = toDelete })).ToList();
        
        return items;
    }
    
    public async Task<int> DeleteOlderThanAsync(DateTime olderThan)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        // Delete non-favorite items older than the specified date
        // Also exclude items that belong to a group (GroupId IS NOT NULL)
        // Metadata will be deleted by CASCADE
        int deleted = await connection.ExecuteAsync(
            """
            DELETE FROM ClipboardItems 
                          WHERE CreatedAt < @OlderThan AND IsFavorite = 0 AND GroupId IS NULL
            """,
            new { OlderThan = olderThan.ToString("O") });
        
        return deleted;
    }
    
    public async Task<int> DeleteExcessItemsAsync(int maxItems)
    {
        await using SqliteConnection connection = await connectionFactory.GetOpenConnectionAsync();
        
        // Get total count of items that can be deleted (not in a group)
        int totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ClipboardItems WHERE GroupId IS NULL");
        
        if (totalCount <= maxItems)
        {
            return 0;
        }
        
        int toDelete = totalCount - maxItems;
        
        // Delete oldest non-favorite items that are NOT in a group
        // Using a subquery to find the IDs of items to delete
        int deleted = await connection.ExecuteAsync(
            """
            DELETE FROM ClipboardItems 
                          WHERE Id IN (
                              SELECT Id FROM ClipboardItems 
                              WHERE IsFavorite = 0 AND GroupId IS NULL
                              ORDER BY CreatedAt ASC 
                              LIMIT @ToDelete
                          )
            """,
            new { ToDelete = toDelete });
        
        return deleted;
    }
    
    private static async Task LoadRelatedDataAsync(Microsoft.Data.Sqlite.SqliteConnection connection, List<ClipboardItem> items)
    {
        if (items.Count == 0) return;
        
        string[] itemIds = items.Select(i => i.Id.ToString()).ToArray();
        
        // Load all metadata in one query using Dapper's list expansion
        string metadataSql = $"SELECT * FROM ClipboardMetadata WHERE ClipboardItemId IN ({string.Join(",", itemIds.Select((_, i) => $"@id{i}"))})";
        DynamicParameters metadataParams = new DynamicParameters();
        for (int i = 0; i < itemIds.Length; i++)
        {
            metadataParams.Add($"id{i}", itemIds[i]);
        }
        IEnumerable<ClipboardMetadata> metadata = await connection.QueryAsync<ClipboardMetadata>(metadataSql, metadataParams);
        
        Dictionary<Guid, ClipboardMetadata> metadataDict = metadata.ToDictionary(m => m.ClipboardItemId);
        
        // Load all groups in one query
        string[] groupIds = items.Where(i => i.GroupId.HasValue).Select(i => i.GroupId!.Value.ToString()).Distinct().ToArray();
        Dictionary<Guid, ClipboardGroup> groupDict = new();
        
        if (groupIds.Length > 0)
        {
            string groupsSql = $"SELECT * FROM ClipboardGroups WHERE Id IN ({string.Join(",", groupIds.Select((_, i) => $"@gid{i}"))})";
            DynamicParameters groupsParams = new DynamicParameters();
            for (int i = 0; i < groupIds.Length; i++)
            {
                groupsParams.Add($"gid{i}", groupIds[i]);
            }
            IEnumerable<ClipboardGroup> groups = await connection.QueryAsync<ClipboardGroup>(groupsSql, groupsParams);
            groupDict = groups.ToDictionary(g => g.Id);
        }
        
        // Assign to items
        foreach (ClipboardItem item in items)
        {
            if (metadataDict.TryGetValue(item.Id, out ClipboardMetadata? itemMetadata))
            {
                item.Metadata = itemMetadata;
            }
            
            if (item.GroupId.HasValue && groupDict.TryGetValue(item.GroupId.Value, out ClipboardGroup? group))
            {
                item.Group = group;
            }
        }
    }
}
