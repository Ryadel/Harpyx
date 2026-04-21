namespace Harpyx.Application.DTOs;

public record RagChunkContextDto(
    Guid DocumentId,
    string FileName,
    int ChunkIndex,
    int? PageNumber,
    string SourceType,
    double? OcrConfidence,
    string Content,
    double Score);

public record RagContextResult(
    string Context,
    IReadOnlyList<RagChunkContextDto> Chunks);
