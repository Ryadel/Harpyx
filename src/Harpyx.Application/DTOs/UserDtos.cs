using System;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record UserDto(
    Guid Id,
    string? ObjectId,
    string? SubjectId,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    string? LastLoginProvider
);

public record UserEditDto(
    Guid? Id,
    string? ObjectId,
    string? SubjectId,
    string Email,
    UserRole Role,
    bool IsActive,
    IReadOnlyList<Guid> TenantIds
);

public record UserSaveRequest(
    Guid? Id,
    string? ObjectId,
    string? SubjectId,
    string Email,
    UserRole Role,
    bool IsActive,
    IReadOnlyList<Guid> TenantIds
);
