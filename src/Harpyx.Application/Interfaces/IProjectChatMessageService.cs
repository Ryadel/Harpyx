using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IProjectChatMessageService
{
    Task<IReadOnlyList<ProjectChatMessageDto>> GetHistoryAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectChatMessageDto>> GetHistoryAsync(Guid projectId, int limit, CancellationToken cancellationToken);
    Task SaveMessagesAsync(Guid projectId, IReadOnlyList<ChatMessageInput> messages, CancellationToken cancellationToken);
    Task PruneHistoryAsync(Guid projectId, CancellationToken cancellationToken);
}
