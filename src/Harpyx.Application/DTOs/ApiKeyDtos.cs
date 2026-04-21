using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record ApiKeyCreateRequest(
    string Name,
    DateTimeOffset? ExpiresAtUtc,
    ApiPermission Permissions
);

public record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPreview,
    string KeyLast4,
    ApiPermission Permissions,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset? LastUsedAtUtc
);

public record ApiKeyCreateResultDto(
    ApiKeyDto ApiKey,
    string PlainTextKey
);

public record ApiKeyValidationResult(
    Guid ApiKeyId,
    Guid UserId,
    string Email,
    string? ObjectId,
    string? SubjectId,
    ApiPermission Permissions
);
