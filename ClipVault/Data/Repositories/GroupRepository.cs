using ClipVault.Models;
using Dapper;

namespace ClipVault.Data.Repositories;

/// <summary>
/// Repository implementation for clipboard groups using Dapper.
/// </summary>
public class GroupRepository : IGroupRepository
{
    private readonly AppDbContext _context;
    
    public GroupRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<ClipboardGroup?> GetByIdAsync(Guid id)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        return await connection.QueryFirstOrDefaultAsync<ClipboardGroup>(
            "SELECT * FROM ClipboardGroups WHERE Id = @Id",
            new { Id = id.ToString() });
    }
    
    public async Task<List<ClipboardGroup>> GetAllAsync()
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        var groups = await connection.QueryAsync<ClipboardGroup>(
            "SELECT * FROM ClipboardGroups ORDER BY SortOrder, Name");
        
        return groups.ToList();
    }
    
    public async Task<ClipboardGroup> AddAsync(ClipboardGroup group)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        // Set sort order to be at the end
        var maxOrder = await connection.ExecuteScalarAsync<int?>(
            "SELECT MAX(SortOrder) FROM ClipboardGroups") ?? 0;
        group.SortOrder = maxOrder + 1;
        
        await connection.ExecuteAsync(@"
            INSERT INTO ClipboardGroups (Id, Name, Color, SortOrder, CreatedAt)
            VALUES (@Id, @Name, @Color, @SortOrder, @CreatedAt)",
            new
            {
                Id = group.Id.ToString(),
                group.Name,
                group.Color,
                group.SortOrder,
                CreatedAt = group.CreatedAt.ToString("O")
            });
        
        return group;
    }
    
    public async Task<ClipboardGroup> UpdateAsync(ClipboardGroup group)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        await connection.ExecuteAsync(@"
            UPDATE ClipboardGroups 
            SET Name = @Name, Color = @Color, SortOrder = @SortOrder
            WHERE Id = @Id",
            new
            {
                Id = group.Id.ToString(),
                group.Name,
                group.Color,
                group.SortOrder
            });
        
        return group;
    }
    
    public async Task DeleteAsync(Guid id)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        
        // Items in this group will have their GroupId set to null due to ON DELETE SET NULL
        await connection.ExecuteAsync(
            "DELETE FROM ClipboardGroups WHERE Id = @Id",
            new { Id = id.ToString() });
    }
    
    public async Task ReorderAsync(List<Guid> orderedIds)
    {
        using var connection = await _context.GetOpenConnectionAsync();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            for (int i = 0; i < orderedIds.Count; i++)
            {
                await connection.ExecuteAsync(
                    "UPDATE ClipboardGroups SET SortOrder = @SortOrder WHERE Id = @Id",
                    new { Id = orderedIds[i].ToString(), SortOrder = i },
                    transaction);
            }
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
