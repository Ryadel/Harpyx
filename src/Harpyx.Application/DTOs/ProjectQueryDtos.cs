namespace Harpyx.Application.DTOs;

public record ProjectQueryRequest(
    string UserPrompt,
    IReadOnlyList<Guid>? DocumentIds = null,
    string? SystemPrompt = null,
    Guid? ModelId = null,
    bool IncludeContext = false);

public record ProjectQueryResponse(
    string UserMessage,
    string AssistantMessage,
    string? Model,
    IReadOnlyList<Guid> DocumentIds,
    IReadOnlyList<RagChunkContextDto>? ContextChunks = null);
