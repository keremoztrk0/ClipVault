using ClipVault.App.Models;

namespace ClipVault.App.Data.Repositories;

/// <summary>
/// Repository interface for clipboard group operations.
/// </summary>
public interface IGroupRepository
{
    Task<ClipboardGroup?> GetByIdAsync(Guid id);
    Task<List<ClipboardGroup>> GetAllAsync();
    Task<ClipboardGroup> AddAsync(ClipboardGroup group);
    Task<ClipboardGroup> UpdateAsync(ClipboardGroup group);
    Task DeleteAsync(Guid id);
    Task ReorderAsync(List<Guid> orderedIds);
}
