using ClipVault.App.Models;

namespace ClipVault.App.Data.Repositories;

/// <summary>
/// Repository interface for clipboard item operations.
/// </summary>
public interface IClipboardRepository
{
    Task<ClipboardItem?> GetByIdAsync(Guid id);
    Task<ClipboardItem?> GetByHashAsync(string contentHash);
    Task<List<ClipboardItem>> GetAllAsync(int? limit = null, int offset = 0);
    Task<List<ClipboardItem>> GetByGroupAsync(Guid groupId, int? limit = null, int offset = 0);
    Task<List<ClipboardItem>> SearchAsync(string searchTerm, ClipboardContentType? contentType = null, Guid? groupId = null);
    Task<ClipboardItem> AddAsync(ClipboardItem item);
    Task<ClipboardItem> UpdateAsync(ClipboardItem item);
    Task DeleteAsync(Guid id);
    Task DeleteAllAsync();
    Task<int> GetCountAsync();
    Task<int> GetCountByGroupAsync(Guid groupId);
    Task<Dictionary<Guid, int>> GetCountsByGroupAsync();
    Task UpdateLastAccessedAsync(Guid id);
}
