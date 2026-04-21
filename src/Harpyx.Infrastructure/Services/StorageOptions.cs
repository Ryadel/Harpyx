namespace Harpyx.Infrastructure.Services;

public record StorageOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool UseSsl { get; init; }
}
