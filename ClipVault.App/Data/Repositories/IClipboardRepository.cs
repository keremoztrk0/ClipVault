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
    
    /// <summary>
    /// Gets items older than the specified date that are eligible for cleanup
    /// (excluding favorites and items in groups).
    /// </summary>
    /// <param name="olderThan">Get items created before this date.</param>
    /// <returns>List of items to be cleaned up.</returns>
    Task<List<ClipboardItem>> GetItemsOlderThanAsync(DateTime olderThan);
    
    /// <summary>
    /// Gets the oldest non-favorite, ungrouped items that exceed the max limit.
    /// Items in groups are excluded.
    /// </summary>
    /// <param name="maxItems">Maximum number of ungrouped items to keep.</param>
    /// <returns>List of excess items to be cleaned up.</returns>
    Task<List<ClipboardItem>> GetExcessItemsAsync(int maxItems);
    
    /// <summary>
    /// Deletes items older than the specified date (excluding favorites and items in groups).
    /// </summary>
    /// <param name="olderThan">Delete items created before this date.</param>
    /// <returns>Number of items deleted.</returns>
    Task<int> DeleteOlderThanAsync(DateTime olderThan);
    
    /// <summary>
    /// Deletes oldest non-favorite, ungrouped items to keep total count at or below the limit.
    /// Items in groups are excluded from deletion.
    /// </summary>
    /// <param name="maxItems">Maximum number of ungrouped items to keep.</param>
    /// <returns>Number of items deleted.</returns>
    Task<int> DeleteExcessItemsAsync(int maxItems);
}
