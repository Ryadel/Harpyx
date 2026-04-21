namespace Harpyx.Application.DTOs;

public record ProjectChatMessageDto(
    Guid Id,
    Guid ProjectId,
    string Role,
    string Content,
    DateTimeOffset MessageTimestamp);

public record ChatMessageInput(
    string Role,
    string Content,
    DateTimeOffset MessageTimestamp);
