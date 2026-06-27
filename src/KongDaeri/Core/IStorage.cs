namespace KongDaeri.Core;

public interface IStorage          // SQLite 수집함
{
    Task SaveAsync(CaptureItem item);
    Task UpdateAsync(CaptureItem item);
    Task<IReadOnlyList<CaptureItem>> ListAsync();
    Task DeleteAsync(Guid id);
}
