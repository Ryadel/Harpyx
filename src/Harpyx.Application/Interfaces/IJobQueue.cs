using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IJobQueue
{
    Task EnqueueParseJobAsync(Guid documentId, CancellationToken cancellationToken);
    Task ConsumeAsync(Func<Guid, CancellationToken, Task> handler, CancellationToken cancellationToken);
}
