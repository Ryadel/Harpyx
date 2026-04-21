using Harpyx.Application.DTOs;
using Harpyx.Application.Defaults;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;

namespace Harpyx.Application.Services;

public class PlatformSettingsService : IPlatformSettingsService
{
    private readonly IPlatformSettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;

    public PlatformSettingsService(IPlatformSettingsRepository settings, IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlatformSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PlatformSettingsDto> SaveAsync(PlatformSettingsSaveRequest request, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        entity.DefaultSystemPrompt = NormalizeDefaultSystemPrompt(request.DefaultSystemPrompt);
        entity.SystemPromptMaxLengthChars = Clamp(request.SystemPromptMaxLengthChars, 1, 100000);
        entity.UserPromptMaxLengthChars = Clamp(request.UserPromptMaxLengthChars, 1, 100000);
        entity.SystemPromptHistoryLimitPerProject = Clamp(request.SystemPromptHistoryLimitPerProject, 0, 200);
        entity.UserPromptHistoryLimitPerProject = Clamp(request.UserPromptHistoryLimitPerProject, 0, 200);
        entity.ChatHistoryLimitPerProject = Clamp(request.ChatHistoryLimitPerProject, 0, 500);
        entity.UserSelfRegistrationEnabled = request.UserSelfRegistrationEnabled;
        entity.QuarantineEnabled = request.QuarantineEnabled;
        entity.UrlDocumentsEnabled = request.UrlDocumentsEnabled;
        entity.MaxFileSizeBytes = Clamp(request.MaxFileSizeBytes, 1L * 1024L * 1024L, 1024L * 1024L * 1024L);
        entity.ContainerMaxNestingDepth = Clamp(request.ContainerMaxNestingDepth, 1, 10);
        entity.ContainerMaxTotalExtractedBytesPerRoot = Clamp(request.ContainerMaxTotalExtractedBytesPerRoot, 25L * 1024L * 1024L, 10L * 1024L * 1024L * 1024L);
        entity.ContainerMaxFilesPerRoot = Clamp(request.ContainerMaxFilesPerRoot, 1, 10000);
        entity.ContainerMaxSingleEntrySizeBytes = Clamp(request.ContainerMaxSingleEntrySizeBytes, 1L * 1024L * 1024L, 2L * 1024L * 1024L * 1024L);
        entity.RagTopK = Clamp(request.RagTopK, 1, 30);
        entity.RagMaxContextChars = Clamp(request.RagMaxContextChars, 2000, 50000);
        entity.RagRrfK = Clamp(request.RagRrfK, 1, 500);
        entity.RagLexicalCandidateK = Clamp(request.RagLexicalCandidateK, 1, 200);
        entity.RagVectorCandidateK = Clamp(request.RagVectorCandidateK, 1, 200);
        entity.RagKeywordMaxCount = Clamp(request.RagKeywordMaxCount, 1, 100);
        entity.RagUseRakeKeywordExtraction = request.RagUseRakeKeywordExtraction;
        entity.RagContextCacheTtlSeconds = Clamp(request.RagContextCacheTtlSeconds, 5, 3600);
        entity.RagUseOpenSearchIndexing = request.RagUseOpenSearchIndexing;
        entity.RagUseOpenSearchRetrieval = request.RagUseOpenSearchRetrieval;
        entity.RagFallbackToSqlRetrievalOnOpenSearchFailure = request.RagFallbackToSqlRetrievalOnOpenSearchFailure;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _settings.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<bool> IsSelfRegistrationAllowedAsync(CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        return entity.UserSelfRegistrationEnabled;
    }

    public async Task<bool> IsQuarantineEnabledAsync(CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        return entity.QuarantineEnabled;
    }

    public async Task<bool> IsUrlDocumentsEnabledAsync(CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        return entity.UrlDocumentsEnabled;
    }

    private async Task<PlatformSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var entity = await _settings.GetAsync(cancellationToken);
        if (entity is not null)
            return entity;

        entity = new PlatformSettings
        {
            DefaultSystemPrompt = PromptDefaults.DefaultSystemPrompt,
            SystemPromptMaxLengthChars = PromptDefaults.SystemPromptMaxLengthChars,
            UserPromptMaxLengthChars = PromptDefaults.UserPromptMaxLengthChars,
            SystemPromptHistoryLimitPerProject = PromptDefaults.SystemPromptHistoryLimitPerProject,
            UserPromptHistoryLimitPerProject = PromptDefaults.UserPromptHistoryLimitPerProject,
            ChatHistoryLimitPerProject = PromptDefaults.ChatHistoryLimitPerProject,
            UserSelfRegistrationEnabled = true,
            QuarantineEnabled = true,
            UrlDocumentsEnabled = true,
            MaxFileSizeBytes = UploadDefaults.MaxFileSizeBytes,
            ContainerMaxNestingDepth = 3,
            ContainerMaxTotalExtractedBytesPerRoot = 500L * 1024L * 1024L,
            ContainerMaxFilesPerRoot = 200,
            ContainerMaxSingleEntrySizeBytes = 100L * 1024L * 1024L,
            RagTopK = 6,
            RagMaxContextChars = 10000,
            RagRrfK = 60,
            RagLexicalCandidateK = 24,
            RagVectorCandidateK = 24,
            RagKeywordMaxCount = 12,
            RagUseRakeKeywordExtraction = true,
            RagContextCacheTtlSeconds = 300,
            RagUseOpenSearchIndexing = true,
            RagUseOpenSearchRetrieval = true,
            RagFallbackToSqlRetrievalOnOpenSearchFailure = true
        };
        await _settings.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private static PlatformSettingsDto ToDto(PlatformSettings settings)
        => new(
            string.IsNullOrWhiteSpace(settings.DefaultSystemPrompt) ? PromptDefaults.DefaultSystemPrompt : settings.DefaultSystemPrompt,
            settings.SystemPromptMaxLengthChars <= 0 ? PromptDefaults.SystemPromptMaxLengthChars : settings.SystemPromptMaxLengthChars,
            settings.UserPromptMaxLengthChars <= 0 ? PromptDefaults.UserPromptMaxLengthChars : settings.UserPromptMaxLengthChars,
            settings.SystemPromptHistoryLimitPerProject < 0 ? PromptDefaults.SystemPromptHistoryLimitPerProject : settings.SystemPromptHistoryLimitPerProject,
            settings.UserPromptHistoryLimitPerProject < 0 ? PromptDefaults.UserPromptHistoryLimitPerProject : settings.UserPromptHistoryLimitPerProject,
            settings.ChatHistoryLimitPerProject <= 0 ? PromptDefaults.ChatHistoryLimitPerProject : settings.ChatHistoryLimitPerProject,
            settings.UserSelfRegistrationEnabled,
            settings.QuarantineEnabled,
            settings.UrlDocumentsEnabled,
            settings.MaxFileSizeBytes <= 0 ? UploadDefaults.MaxFileSizeBytes : settings.MaxFileSizeBytes,
            settings.ContainerMaxNestingDepth <= 0 ? 3 : settings.ContainerMaxNestingDepth,
            settings.ContainerMaxTotalExtractedBytesPerRoot <= 0 ? 500L * 1024L * 1024L : settings.ContainerMaxTotalExtractedBytesPerRoot,
            settings.ContainerMaxFilesPerRoot <= 0 ? 200 : settings.ContainerMaxFilesPerRoot,
            settings.ContainerMaxSingleEntrySizeBytes <= 0 ? 100L * 1024L * 1024L : settings.ContainerMaxSingleEntrySizeBytes,
            settings.RagTopK,
            settings.RagMaxContextChars,
            settings.RagRrfK,
            settings.RagLexicalCandidateK,
            settings.RagVectorCandidateK,
            settings.RagKeywordMaxCount,
            settings.RagUseRakeKeywordExtraction,
            settings.RagContextCacheTtlSeconds,
            settings.RagUseOpenSearchIndexing,
            settings.RagUseOpenSearchRetrieval,
            settings.RagFallbackToSqlRetrievalOnOpenSearchFailure);

    private static int Clamp(int value, int min, int max)
        => Math.Min(max, Math.Max(min, value));

    private static long Clamp(long value, long min, long max)
        => Math.Min(max, Math.Max(min, value));

    private static string NormalizeDefaultSystemPrompt(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? PromptDefaults.DefaultSystemPrompt
            : value.Trim();
}
