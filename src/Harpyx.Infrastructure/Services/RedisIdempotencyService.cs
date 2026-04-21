using Harpyx.Application.Interfaces;
using Harpyx.Shared;
using StackExchange.Redis;

namespace Harpyx.Infrastructure.Services;

public class RedisIdempotencyService : IIdempotencyService
{
    private readonly IDatabase _database;

    public RedisIdempotencyService(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        var fullKey = HarpyxConstants.IdempotencyPrefix + key;
        return await _database.KeyExistsAsync(fullKey);
    }

    public async Task StoreAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var fullKey = HarpyxConstants.IdempotencyPrefix + key;
        await _database.StringSetAsync(fullKey, "1", ttl);
    }
}
