using Harpyx.Application.DTOs;
using Harpyx.Application.Defaults;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Controllers;

[ApiController]
[Route("api/projects")]
[IgnoreAntiforgeryToken]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;
    private readonly IWorkspaceService _workspaces;
    private readonly IDocumentService _documents;
    private readonly ILlmClientResolver _llmResolver;
    private readonly IRagRetrievalService _ragRetrieval;
    private readonly IProjectChatMessageService _chatMessages;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly IUserService _users;
    private readonly ITenantScopeService _tenantScope;

    public ProjectsController(
        IProjectService projects,
        IWorkspaceService workspaces,
        IDocumentService documents,
        ILlmClientResolver llmResolver,
        IRagRetrievalService ragRetrieval,
        IProjectChatMessageService chatMessages,
        IPlatformSettingsService platformSettings,
        IUserService users,
        ITenantScopeService tenantScope)
    {
        _projects = projects;
        _workspaces = workspaces;
        _documents = documents;
        _llmResolver = llmResolver;
        _ragRetrieval = ragRetrieval;
        _chatMessages = chatMessages;
        _platformSettings = platformSettings;
        _users = users;
        _tenantScope = tenantScope;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetAll([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.QueryProjects))
            return Forbid();

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        if (scope.EffectiveTenantIds.Count == 0)
            return Ok(Array.Empty<ProjectDto>());

        IReadOnlyList<Guid> effectiveTenantIds = scope.EffectiveTenantIds;
        if (tenantId is Guid requestedTenantId)
        {
            if (!scope.CanAccessTenant(requestedTenantId))
                return Forbid();

            if (!scope.IsAllTenants && scope.CurrentTenantId != requestedTenantId)
                return Forbid();

            effectiveTenantIds = new[] { requestedTenantId };
        }

        var projects = await _projects.GetAllAsync(effectiveTenantIds, cancellationToken);
        return Ok(projects);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.CreateProjects))
            return Forbid();

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        var workspace = await _workspaces.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (workspace is null)
            return NotFound(new { error = "Workspace not found." });

        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();
        if (!scope.CanManageProjects(workspace.TenantId))
            return Forbid();

        var userId = await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            cancellationToken);
        if (userId is null)
            return Forbid();

        var safeRequest = request with { CreatedByUserId = userId.Value };
        var project = await _projects.CreateAsync(safeRequest, cancellationToken);
        return Ok(project);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, ProjectDto project, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.CreateProjects))
            return Forbid();

        if (id != project.Id)
        {
            return BadRequest();
        }

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);

        var existing = await _projects.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return NotFound(new { error = "Project not found." });

        var existingWorkspace = await _workspaces.GetByIdAsync(existing.WorkspaceId, cancellationToken);
        if (existingWorkspace is null || !scope.IsTenantInScope(existingWorkspace.TenantId))
            return Forbid();
        if (!scope.CanManageProjects(existingWorkspace.TenantId))
            return Forbid();

        var targetWorkspace = await _workspaces.GetByIdAsync(project.WorkspaceId, cancellationToken);
        if (targetWorkspace is null)
            return NotFound(new { error = "Workspace not found." });

        if (!scope.IsTenantInScope(targetWorkspace.TenantId))
            return Forbid();

        var updated = await _projects.UpdateAsync(project, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/query")]
    public async Task<ActionResult<ProjectQueryResponse>> Query(Guid id, [FromBody] ProjectQueryRequest request, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.QueryProjects))
            return Forbid();

        var userPrompt = NormalizePrompt(request.UserPrompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
            return BadRequest(new { error = "UserPrompt is required." });

        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project not found." });

        var workspace = await _workspaces.GetByIdAsync(project.WorkspaceId, cancellationToken);
        if (workspace is null)
            return NotFound(new { error = "Workspace not found." });

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();
        if (!scope.CanUseChat(workspace.TenantId))
            return Forbid();

        var userId = await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            cancellationToken);
        if (userId is null)
            return Unauthorized();

        var settings = await _platformSettings.GetAsync(cancellationToken);
        var maxSystemPromptLength = settings.SystemPromptMaxLengthChars > 0
            ? settings.SystemPromptMaxLengthChars
            : PromptDefaults.SystemPromptMaxLengthChars;
        var maxUserPromptLength = settings.UserPromptMaxLengthChars > 0
            ? settings.UserPromptMaxLengthChars
            : PromptDefaults.UserPromptMaxLengthChars;

        var defaultSystemPrompt = string.IsNullOrWhiteSpace(settings.DefaultSystemPrompt)
            ? PromptDefaults.DefaultSystemPrompt
            : settings.DefaultSystemPrompt;
        var systemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? defaultSystemPrompt
            : request.SystemPrompt.Trim();

        if (systemPrompt.Length > maxSystemPromptLength)
            return BadRequest(new { error = $"SystemPrompt exceeds the maximum length ({maxSystemPromptLength} characters)." });
        if (userPrompt.Length > maxUserPromptLength)
            return BadRequest(new { error = $"UserPrompt exceeds the maximum length ({maxUserPromptLength} characters)." });

        var documents = await _documents.GetByProjectAsync(id, cancellationToken);
        var selectableDocumentIds = documents
            .Where(IsDocumentSelectable)
            .Select(d => d.Id)
            .ToHashSet();

        IReadOnlyList<Guid> selectedDocumentIds;
        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
        {
            selectedDocumentIds = selectableDocumentIds.ToList();
        }
        else
        {
            var requestedIds = request.DocumentIds.Distinct().ToList();
            var hasInvalidDocument = requestedIds.Any(docId => !selectableDocumentIds.Contains(docId));
            if (hasInvalidDocument)
                return BadRequest(new { error = "One or more DocumentIds are invalid, unavailable, or not selectable." });

            selectedDocumentIds = requestedIds;
        }

        if (selectedDocumentIds.Count == 0)
            return BadRequest(new { error = "No selectable documents found. Upload or enable at least one valid document." });

        var defaultChatModelId = ResolveEffectiveDefaultChatModelId(project, workspace);
        var resolve = await _llmResolver.ResolveAsync(
            userId.Value,
            request.ModelId,
            defaultChatModelId,
            cancellationToken);
        if (!resolve.IsConfigured || resolve.Client is null)
            return BadRequest(new { error = "No configured Chat model found for this request." });

        var rag = await _ragRetrieval.BuildContextAsync(id, selectedDocumentIds, userPrompt, cancellationToken);
        var effectiveSystemPrompt = $"{systemPrompt}\n\nRAG context:\n{rag.Context}";

        var result = await resolve.Client.ChatCompletionAsync(effectiveSystemPrompt, userPrompt, resolve.Model, cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return BadRequest(new { error = result.Error ?? "LLM response failed." });

        var assistantMessage = result.Content.Trim();
        await _chatMessages.SaveMessagesAsync(id, new[]
        {
            new ChatMessageInput("user", userPrompt, DateTimeOffset.UtcNow),
            new ChatMessageInput("assistant", assistantMessage, DateTimeOffset.UtcNow)
        }, cancellationToken);
        await _chatMessages.PruneHistoryAsync(id, cancellationToken);

        return Ok(new ProjectQueryResponse(
            userPrompt,
            assistantMessage,
            resolve.Model,
            selectedDocumentIds,
            request.IncludeContext ? rag.Chunks : null));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.DeleteProjects))
            return Forbid();

        var project = await _projects.GetByIdAsync(id, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project not found." });

        var workspace = await _workspaces.GetByIdAsync(project.WorkspaceId, cancellationToken);
        if (workspace is null)
            return NotFound(new { error = "Workspace not found." });

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();
        if (!scope.CanManageProjects(workspace.TenantId))
            return Forbid();

        await _projects.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private static Guid? ResolveEffectiveDefaultChatModelId(ProjectDto project, WorkspaceDto workspace)
    {
        return project.ChatLlmOverride switch
        {
            LlmFeatureOverride.Enabled => project.ChatModelId,
            LlmFeatureOverride.Disabled => null,
            _ => workspace.IsChatLlmEnabled ? workspace.ChatModelId : null
        };
    }

    private static bool IsDocumentSelectable(DocumentDto document)
        => document.State is not (DocumentState.Quarantined or DocumentState.Rejected);

    private static string NormalizePrompt(string? value) => value?.Trim() ?? string.Empty;
}
