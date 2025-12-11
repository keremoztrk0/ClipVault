using ClipVault.Models;

namespace ClipVault.Data.Repositories;

/// <summary>
/// Repository interface for application settings.
/// </summary>
public interface ISettingsRepository
{
    Task<AppSettings> GetSettingsAsync();
    Task<AppSettings> UpdateSettingsAsync(AppSettings settings);
}
