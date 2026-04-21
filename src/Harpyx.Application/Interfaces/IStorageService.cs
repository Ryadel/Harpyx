namespace Harpyx.Application.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
