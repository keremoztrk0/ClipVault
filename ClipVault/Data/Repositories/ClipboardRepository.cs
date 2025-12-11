using ClipVault.Models;
using Dapper;

namespace ClipVault.Data.Repositories;

/// <summary>
/// Repository implementation for clipboard items using Dapper.
/// </summary>
public class ClipboardRepository : IClipboardRepository
{
    private readonly AppDbContext _context;
    
    public ClipboardRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<ClipboardItem?> GetByIdAsync(Guid id)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        var item = await connection.QueryFirstOrDefaultAsync<ClipboardItem>(
            "SELECT * FROM ClipboardItems WHERE Id = @Id",
            new { Id = id.ToString() });
        
        if (item != null)
        {
            item.Metadata = await connection.QueryFirstOrDefaultAsync<ClipboardMetadata>(
                "SELECT * FROM ClipboardMetadata WHERE ClipboardItemId = @ClipboardItemId",
                new { ClipboardItemId = id.ToString() });
            
            if (item.GroupId.HasValue)
            {
                item.Group = await connection.QueryFirstOrDefaultAsync<ClipboardGroup>(
                    "SELECT * FROM ClipboardGroups WHERE Id = @Id",
                    new { Id = item.GroupId.Value.ToString() });
            }
        }
        
        return item;
    }
    
    public async Task<ClipboardItem?> GetByHashAsync(string contentHash)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        var item = await connection.QueryFirstOrDefaultAsync<ClipboardItem>(
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
        using var connection = await _context.GetOpenConnectionAsync();
        
        var sql = "SELECT * FROM ClipboardItems ORDER BY CreatedAt DESC";
        
        if (limit.HasValue)
        {
            sql += " LIMIT @Limit OFFSET @Offset";
        }
        else if (offset > 0)
        {
            sql += " LIMIT -1 OFFSET @Offset";
        }
        
        var items = (await connection.QueryAsync<ClipboardItem>(sql, new { Limit = limit, Offset = offset })).ToList();
        
        await LoadRelatedDataAsync(connection, items);
        
        return items;
    }
    
    public async Task<List<ClipboardItem>> GetByGroupAsync(Guid groupId, int? limit = null, int offset = 0)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        var sql = "SELECT * FROM ClipboardItems WHERE GroupId = @GroupId ORDER BY CreatedAt DESC";
        
        if (limit.HasValue)
        {
            sql += " LIMIT @Limit OFFSET @Offset";
        }
        
        var items = (await connection.QueryAsync<ClipboardItem>(sql, 
            new { GroupId = groupId.ToString(), Limit = limit, Offset = offset })).ToList();
        
        await LoadRelatedDataAsync(connection, items);
        
        return items;
    }
    
    public async Task<List<ClipboardItem>> SearchAsync(
        string? searchTerm, 
        ClipboardContentType? contentType = null, 
        Guid? groupId = null)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        var sql = "SELECT * FROM ClipboardItems WHERE 1=1";
        var parameters = new DynamicParameters();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql += @" AND (
                PreviewText LIKE @SearchTerm 
                OR TextContent LIKE @SearchTerm 
                OR SourceApplication LIKE @SearchTerm)";
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
        
        var items = (await connection.QueryAsync<ClipboardItem>(sql, parameters)).ToList();
        
        await LoadRelatedDataAsync(connection, items);
        
        return items;
    }
    
    public async Task<ClipboardItem> AddAsync(ClipboardItem item)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        using var transaction = connection.BeginTransaction();
        
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
                await connection.ExecuteAsync(@"
                    INSERT INTO ClipboardMetadata (Id, ClipboardItemId, CharacterCount, WordCount, LineCount, FileName, FileSize, FileExtension, OriginalPath, DurationMs, Width, Height, MimeType, FileCount)
                    VALUES (@Id, @ClipboardItemId, @CharacterCount, @WordCount, @LineCount, @FileName, @FileSize, @FileExtension, @OriginalPath, @DurationMs, @Width, @Height, @MimeType, @FileCount)",
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
        using var connection = await _context.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync(@"
            UPDATE ClipboardItems 
            SET ContentType = @ContentType, TextContent = @TextContent, FilePath = @FilePath, 
                PreviewText = @PreviewText, SourceApplication = @SourceApplication, GroupId = @GroupId,
                IsFavorite = @IsFavorite, LastAccessedAt = @LastAccessedAt, ContentHash = @ContentHash
            WHERE Id = @Id",
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
        using var connection = await _context.GetOpenConnectionAsync();
        
        // Metadata will be deleted by CASCADE
        await connection.ExecuteAsync(
            "DELETE FROM ClipboardItems WHERE Id = @Id",
            new { Id = id.ToString() });
    }
    
    public async Task DeleteAllAsync()
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync("DELETE FROM ClipboardMetadata");
        await connection.ExecuteAsync("DELETE FROM ClipboardItems");
    }
    
    public async Task<int> GetCountAsync()
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ClipboardItems");
    }
    
    public async Task UpdateLastAccessedAsync(Guid id)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync(
            "UPDATE ClipboardItems SET LastAccessedAt = @LastAccessedAt WHERE Id = @Id",
            new { Id = id.ToString(), LastAccessedAt = DateTime.UtcNow.ToString("O") });
    }
    
    private static async Task LoadRelatedDataAsync(Microsoft.Data.Sqlite.SqliteConnection connection, List<ClipboardItem> items)
    {
        if (items.Count == 0) return;
        
        var itemIds = items.Select(i => i.Id.ToString()).ToArray();
        
        // Load all metadata in one query using Dapper's list expansion
        var metadataSql = $"SELECT * FROM ClipboardMetadata WHERE ClipboardItemId IN ({string.Join(",", itemIds.Select((_, i) => $"@id{i}"))})";
        var metadataParams = new DynamicParameters();
        for (int i = 0; i < itemIds.Length; i++)
        {
            metadataParams.Add($"id{i}", itemIds[i]);
        }
        var metadata = await connection.QueryAsync<ClipboardMetadata>(metadataSql, metadataParams);
        
        var metadataDict = metadata.ToDictionary(m => m.ClipboardItemId);
        
        // Load all groups in one query
        var groupIds = items.Where(i => i.GroupId.HasValue).Select(i => i.GroupId!.Value.ToString()).Distinct().ToArray();
        Dictionary<Guid, ClipboardGroup> groupDict = new();
        
        if (groupIds.Length > 0)
        {
            var groupsSql = $"SELECT * FROM ClipboardGroups WHERE Id IN ({string.Join(",", groupIds.Select((_, i) => $"@gid{i}"))})";
            var groupsParams = new DynamicParameters();
            for (int i = 0; i < groupIds.Length; i++)
            {
                groupsParams.Add($"gid{i}", groupIds[i]);
            }
            var groups = await connection.QueryAsync<ClipboardGroup>(groupsSql, groupsParams);
            groupDict = groups.ToDictionary(g => g.Id);
        }
        
        // Assign to items
        foreach (var item in items)
        {
            if (metadataDict.TryGetValue(item.Id, out var itemMetadata))
            {
                item.Metadata = itemMetadata;
            }
            
            if (item.GroupId.HasValue && groupDict.TryGetValue(item.GroupId.Value, out var group))
            {
                item.Group = group;
            }
        }
    }
}
