using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record DocumentDto(
    Guid Id, Guid ProjectId, string FileName, string ContentType,
    long SizeBytes, int Version, DocumentState State,
    Guid? ParentDocumentId, Guid? RootContainerDocumentId,
    int NestingLevel, string? ContainerPath,
    bool IsContainer, DocumentContainerType ContainerType,
    DocumentExtractionState ExtractionState, Guid? OriginatingUploadId,
    string? SourceUrl);

public record UploadDocumentRequest(Guid UploadedByUserId, Guid ProjectId, string FileName, string ContentType, Stream Content, long SizeBytes);

public record AddUrlDocumentRequest(Guid ProjectId, Guid UploadedByUserId, string Url);
