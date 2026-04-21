using System.Security.Cryptography;
using System.Text;
using Harpyx.Application.Defaults;
using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class ProjectPromptService : IProjectPromptService
{
    private readonly IProjectPromptRepository _prompts;
    private readonly IProjectService _projects;
    private readonly IPlatformSettingsService _settings;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectPromptService(
        IProjectPromptRepository prompts,
        IProjectService projects,
        IPlatformSettingsService settings,
        IUnitOfWork unitOfWork)
    {
        _prompts = prompts;
        _projects = projects;
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectPromptCollectionDto> GetProjectPromptsAsync(
        Guid projectId,
        ProjectPromptType promptType,
        CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var historyLimit = ResolveHistoryLimit(settings, promptType);
        var prompts = await _prompts.GetByProjectAndTypeAsync(projectId, promptType, cancellationToken);

        var favorites = prompts
            .Where(p => p.IsFavorite)
            .OrderByDescending(p => p.LastUsedAt)
            .ThenByDescending(p => p.CreatedAt)
            .Select(ToDto)
            .ToList();

        var history = prompts
            .Where(p => !p.IsFavorite)
            .OrderByDescending(p => p.LastUsedAt)
            .ThenByDescending(p => p.CreatedAt)
            .Take(historyLimit)
            .Select(ToDto)
            .ToList();

        return new ProjectPromptCollectionDto(favorites, history);
    }

    public async Task<ProjectPromptDto?> GetByIdAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var prompt = await _prompts.GetByIdAsync(promptId, cancellationToken);
        return prompt is null ? null : ToDto(prompt);
    }

    public async Task<ProjectPromptDto?> GetLastUsedAsync(
        Guid projectId,
        ProjectPromptType promptType,
        CancellationToken cancellationToken)
    {
        var prompt = await _prompts.GetLastUsedAsync(projectId, promptType, cancellationToken);
        return prompt is null ? null : ToDto(prompt);
    }

    public async Task<ProjectPromptDto?> SavePromptUsageAsync(
        Guid projectId,
        ProjectPromptType promptType,
        string? content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var normalizedContent = content.Trim();
        var settings = await _settings.GetAsync(cancellationToken);
        var maxLength = ResolveMaxLength(settings, promptType);
        if (normalizedContent.Length > maxLength)
            throw new InvalidOperationException($"{promptType} prompt cannot exceed {maxLength} characters.");

        var now = DateTimeOffset.UtcNow;
        var contentHash = ComputeContentHash(normalizedContent);
        var existing = await _prompts.GetExactMatchAsync(
            projectId,
            promptType,
            contentHash,
            normalizedContent,
            cancellationToken);

        ProjectPrompt trackedPrompt;
        if (existing is not null)
        {
            existing.LastUsedAt = now;
            existing.UpdatedAt = now;
            _prompts.Update(existing);
            trackedPrompt = existing;
        }
        else
        {
            trackedPrompt = new ProjectPrompt
            {
                ProjectId = projectId,
                PromptType = promptType,
                Content = normalizedContent,
                ContentHash = contentHash,
                IsFavorite = false,
                LastUsedAt = now
            };
            await _prompts.AddAsync(trackedPrompt, cancellationToken);
        }

        var historyLimit = ResolveHistoryLimit(settings, promptType);
        await EnforceHistoryLimitAsync(projectId, promptType, historyLimit, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _projects.TouchLifetimeAsync(projectId, cancellationToken);

        return ToDto(trackedPrompt);
    }

    public async Task<bool> ToggleFavoriteAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var prompt = await _prompts.GetByIdAsync(promptId, cancellationToken);
        if (prompt is null)
            return false;

        var settings = await _settings.GetAsync(cancellationToken);
        prompt.IsFavorite = !prompt.IsFavorite;
        prompt.LastUsedAt = DateTimeOffset.UtcNow;
        prompt.UpdatedAt = prompt.LastUsedAt;
        _prompts.Update(prompt);

        if (!prompt.IsFavorite)
        {
            var historyLimit = ResolveHistoryLimit(settings, prompt.PromptType);
            await EnforceHistoryLimitAsync(prompt.ProjectId, prompt.PromptType, historyLimit, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _projects.TouchLifetimeAsync(prompt.ProjectId, cancellationToken);
        return true;
    }

    private async Task EnforceHistoryLimitAsync(
        Guid projectId,
        ProjectPromptType promptType,
        int historyLimit,
        CancellationToken cancellationToken)
    {
        if (historyLimit < 0)
            historyLimit = 0;

        var prompts = await _prompts.GetByProjectAndTypeAsync(projectId, promptType, cancellationToken);
        var overflow = prompts
            .Where(p => !p.IsFavorite)
            .OrderByDescending(p => p.LastUsedAt)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(historyLimit)
            .ToList();

        if (overflow.Count == 0)
            return;

        _prompts.RemoveRange(overflow);
    }

    private static int ResolveMaxLength(PlatformSettingsDto settings, ProjectPromptType promptType)
    {
        var configured = promptType == ProjectPromptType.System
            ? settings.SystemPromptMaxLengthChars
            : settings.UserPromptMaxLengthChars;
        var fallback = promptType == ProjectPromptType.System
            ? PromptDefaults.SystemPromptMaxLengthChars
            : PromptDefaults.UserPromptMaxLengthChars;
        return Math.Max(1, configured > 0 ? configured : fallback);
    }

    private static int ResolveHistoryLimit(PlatformSettingsDto settings, ProjectPromptType promptType)
    {
        var configured = promptType == ProjectPromptType.System
            ? settings.SystemPromptHistoryLimitPerProject
            : settings.UserPromptHistoryLimitPerProject;
        var fallback = promptType == ProjectPromptType.System
            ? PromptDefaults.SystemPromptHistoryLimitPerProject
            : PromptDefaults.UserPromptHistoryLimitPerProject;
        return Math.Max(0, configured >= 0 ? configured : fallback);
    }

    private static string ComputeContentHash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static ProjectPromptDto ToDto(ProjectPrompt prompt)
        => new(
            prompt.Id,
            prompt.ProjectId,
            prompt.PromptType,
            prompt.Content,
            prompt.IsFavorite,
            prompt.LastUsedAt);
}
