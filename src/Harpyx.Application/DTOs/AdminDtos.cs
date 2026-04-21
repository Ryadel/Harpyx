using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record PlatformSettingsDto(
    string DefaultSystemPrompt,
    int SystemPromptMaxLengthChars,
    int UserPromptMaxLengthChars,
    int SystemPromptHistoryLimitPerProject,
    int UserPromptHistoryLimitPerProject,
    int ChatHistoryLimitPerProject,
    bool UserSelfRegistrationEnabled,
    bool QuarantineEnabled,
    bool UrlDocumentsEnabled,
    long MaxFileSizeBytes,
    int ContainerMaxNestingDepth,
    long ContainerMaxTotalExtractedBytesPerRoot,
    int ContainerMaxFilesPerRoot,
    long ContainerMaxSingleEntrySizeBytes,
    int RagTopK,
    int RagMaxContextChars,
    int RagRrfK,
    int RagLexicalCandidateK,
    int RagVectorCandidateK,
    int RagKeywordMaxCount,
    bool RagUseRakeKeywordExtraction,
    int RagContextCacheTtlSeconds,
    bool RagUseOpenSearchIndexing,
    bool RagUseOpenSearchRetrieval,
    bool RagFallbackToSqlRetrievalOnOpenSearchFailure);

public record PlatformSettingsSaveRequest(
    string? DefaultSystemPrompt,
    int SystemPromptMaxLengthChars,
    int UserPromptMaxLengthChars,
    int SystemPromptHistoryLimitPerProject,
    int UserPromptHistoryLimitPerProject,
    int ChatHistoryLimitPerProject,
    bool UserSelfRegistrationEnabled,
    bool QuarantineEnabled,
    bool UrlDocumentsEnabled,
    long MaxFileSizeBytes,
    int ContainerMaxNestingDepth,
    long ContainerMaxTotalExtractedBytesPerRoot,
    int ContainerMaxFilesPerRoot,
    long ContainerMaxSingleEntrySizeBytes,
    int RagTopK,
    int RagMaxContextChars,
    int RagRrfK,
    int RagLexicalCandidateK,
    int RagVectorCandidateK,
    int RagKeywordMaxCount,
    bool RagUseRakeKeywordExtraction,
    int RagContextCacheTtlSeconds,
    bool RagUseOpenSearchIndexing,
    bool RagUseOpenSearchRetrieval,
    bool RagFallbackToSqlRetrievalOnOpenSearchFailure);

public record InviteUserRequest(
    string Email,
    UserInvitationScope Scope = UserInvitationScope.SelfRegistration,
    Guid? TenantId = null,
    int ExpiresInDays = 7,
    string? RegistrationUrl = null);

public record UserInvitationDto(
    Guid Id,
    string Email,
    UserInvitationScope Scope,
    UserInvitationStatus Status,
    Guid? TenantId,
    string? TenantName,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string InvitedByEmail,
    DateTimeOffset? AcceptedAt);
