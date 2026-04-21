namespace Harpyx.Application.Interfaces;

public interface IUrlFetcher
{
    Task<UrlFetchResult> FetchAsync(string url, CancellationToken cancellationToken);
}

public record UrlFetchResult(
    Stream Content,
    string? ContentType,
    long? ContentLength,
    string? Title);
