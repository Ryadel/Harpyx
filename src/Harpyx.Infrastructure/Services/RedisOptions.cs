namespace Harpyx.Infrastructure.Services;

public record RedisOptions
{
    public string ConnectionString { get; init; } = "localhost:6379";
}
