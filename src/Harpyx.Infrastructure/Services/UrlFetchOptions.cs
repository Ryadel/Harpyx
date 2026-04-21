namespace Harpyx.Infrastructure.Services;

public class UrlFetchOptions
{
    public int TimeoutSeconds { get; set; } = 30;
    public long MaxContentBytes { get; set; } = 50L * 1024L * 1024L;
    public bool AllowHttp { get; set; } = false;
}
