using Harpyx.Application.Defaults;
using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;

namespace Harpyx.Application.Services;

public class ProjectChatMessageService : IProjectChatMessageService
{
    private readonly IProjectChatMessageRepository _messages;
    private readonly IProjectService _projects;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectChatMessageService(
        IProjectChatMessageRepository messages,
        IProjectService projects,
        IPlatformSettingsService platformSettings,
        IUnitOfWork unitOfWork)
    {
        _messages = messages;
        _projects = projects;
        _platformSettings = platformSettings;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ProjectChatMessageDto>> GetHistoryAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var settings = await _platformSettings.GetAsync(cancellationToken);
        return await GetHistoryAsync(projectId, settings.ChatHistoryLimitPerProject, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectChatMessageDto>> GetHistoryAsync(
        Guid projectId,
        int limit,
        CancellationToken cancellationToken)
    {
        var messages = await _messages.GetByProjectAsync(projectId, limit, cancellationToken);
        return messages.Select(m => new ProjectChatMessageDto(
            m.Id, m.ProjectId, m.Role, m.Content, m.MessageTimestamp)).ToList();
    }

    public async Task SaveMessagesAsync(
        Guid projectId,
        IReadOnlyList<ChatMessageInput> messages,
        CancellationToken cancellationToken)
    {
        var entities = messages.Select(m => new ProjectChatMessage
        {
            ProjectId = projectId,
            Role = m.Role,
            Content = m.Content,
            MessageTimestamp = m.MessageTimestamp
        }).ToList();

        await _messages.AddRangeAsync(entities, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _projects.TouchLifetimeAsync(projectId, cancellationToken);
    }

    public async Task PruneHistoryAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var settings = await _platformSettings.GetAsync(cancellationToken);
        var limit = settings.ChatHistoryLimitPerProject;
        if (limit <= 0)
            return;

        var count = await _messages.CountByProjectAsync(projectId, cancellationToken);
        if (count <= limit)
            return;

        var all = await _messages.GetByProjectAsync(projectId, 0, cancellationToken);
        var toRemove = all.Take(count - limit).ToList();
        if (toRemove.Count > 0)
        {
            _messages.RemoveRange(toRemove);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
