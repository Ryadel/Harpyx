namespace Harpyx.Application.Interfaces;

public interface IIdempotencyService
{
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);
    Task StoreAsync(string key, TimeSpan ttl, CancellationToken cancellationToken);
}
