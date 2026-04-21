using Harpyx.Application.Interfaces;
using Harpyx.Shared;
using Minio;
using Minio.DataModel.Args;

namespace Harpyx.Infrastructure.Services;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;

    public MinioStorageService(IMinioClient client)
    {
        _client = client;
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var bucketExists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(HarpyxConstants.MinioBucketName), cancellationToken);
        if (!bucketExists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(HarpyxConstants.MinioBucketName), cancellationToken);
        }

        var objectKey = $"{Guid.NewGuid():N}-{fileName}";
        var putArgs = new PutObjectArgs()
            .WithBucket(HarpyxConstants.MinioBucketName)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await _client.PutObjectAsync(putArgs, cancellationToken);
        return objectKey;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var memory = new MemoryStream();
        var getArgs = new GetObjectArgs()
            .WithBucket(HarpyxConstants.MinioBucketName)
            .WithObject(storageKey)
            .WithCallbackStream(stream => stream.CopyTo(memory));

        await _client.GetObjectAsync(getArgs, cancellationToken);
        memory.Position = 0;
        return memory;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var removeArgs = new RemoveObjectArgs()
            .WithBucket(HarpyxConstants.MinioBucketName)
            .WithObject(storageKey);

        await _client.RemoveObjectAsync(removeArgs, cancellationToken);
    }
}
