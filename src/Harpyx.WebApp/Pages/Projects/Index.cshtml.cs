using Harpyx.Application.DTOs;
using Harpyx.Application.Filters;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Harpyx.WebApp.Pages.Projects;

public class IndexModel : PageModel
{
    private readonly IProjectService _projects;
    private readonly IDocumentService _documents;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly IWorkspaceService _workspaces;
    private readonly ITenantService _tenants;
    private readonly IProjectChatMessageService _chatMessages;
    private readonly ITenantScopeService _tenantScope;

    public IndexModel(
        IProjectService projects,
        IDocumentService documents,
        IPlatformSettingsService platformSettings,
        IWorkspaceService workspaces,
        ITenantService tenants,
        IProjectChatMessageService chatMessages,
        ITenantScopeService tenantScope)
    {
        _projects = projects;
        _documents = documents;
        _platformSettings = platformSettings;
        _workspaces = workspaces;
        _tenants = tenants;
        _chatMessages = chatMessages;
        _tenantScope = tenantScope;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? FilterTenantId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? FilterWorkspaceId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "name";

    public IReadOnlyList<ProjectOperationalRow> Rows { get; private set; } = Array.Empty<ProjectOperationalRow>();
    public IEnumerable<SelectListItem> TenantOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> WorkspaceOptions { get; private set; } = Array.Empty<SelectListItem>();

    public record ProjectOperationalRow(
        Guid Id,
        string Name,
        string? Description,
        Guid WorkspaceId,
        string WorkspaceName,
        Guid TenantId,
        string TenantName,
        int TotalDocs,
        int IndexedDocs,
        int FailedDocs,
        int ProcessingDocs,
        int ChatMessages,
        DateTimeOffset? LastActivity,
        string Status);

    public async Task OnGetAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);

        var allWorkspaces = await _workspaces.GetAllAsync(scope.EffectiveTenantIds, HttpContext.RequestAborted);
        var allProjects = await _projects.GetAllAsync(scope.EffectiveTenantIds, HttpContext.RequestAborted);

        var tenantDtos = await _tenants.GetAsync(
            new TenantFilter { Ids = scope.EffectiveTenantIds, IncludeVisibleToAllUsers = true },
            HttpContext.RequestAborted);

        var tenantNamesById = tenantDtos
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var workspaceMap = allWorkspaces.ToDictionary(w => w.Id);

        // Build filter options
        TenantOptions = tenantDtos
            .DistinctBy(t => t.Id)
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == FilterTenantId));

        WorkspaceOptions = allWorkspaces
            .Where(w => FilterTenantId is null || w.TenantId == FilterTenantId)
            .OrderBy(w => w.Name)
            .Select(w => new SelectListItem(w.Name, w.Id.ToString(), w.Id == FilterWorkspaceId));

        // Apply filters
        var filteredProjects = allProjects.AsEnumerable();

        if (FilterWorkspaceId is not null)
        {
            filteredProjects = filteredProjects.Where(p => p.WorkspaceId == FilterWorkspaceId);
        }
        else if (FilterTenantId is not null)
        {
            var workspaceIdsInTenant = allWorkspaces
                .Where(w => w.TenantId == FilterTenantId)
                .Select(w => w.Id)
                .ToHashSet();
            filteredProjects = filteredProjects.Where(p => workspaceIdsInTenant.Contains(p.WorkspaceId));
        }

        var projectList = filteredProjects.ToList();
        var settings = await _platformSettings.GetAsync(HttpContext.RequestAborted);
        var chatHistoryLimit = settings.ChatHistoryLimitPerProject;

        var rows = new List<ProjectOperationalRow>(projectList.Count);

        foreach (var project in projectList)
        {
            var docs = await _documents.GetByProjectAsync(project.Id, HttpContext.RequestAborted);
            var chatHistory = await _chatMessages.GetHistoryAsync(project.Id, chatHistoryLimit, HttpContext.RequestAborted);

            var workspace = workspaceMap.GetValueOrDefault(project.WorkspaceId);
            var tenantId = workspace?.TenantId ?? Guid.Empty;
            var workspaceName = workspace?.Name ?? "-";
            var tenantName = tenantId != Guid.Empty && tenantNamesById.TryGetValue(tenantId, out var tn) ? tn : "-";

            var totalDocs = docs.Count;
            var indexedDocs = docs.Count(d => d.State == DocumentState.Completed);
            var failedDocs = docs.Count(d => d.State == DocumentState.Failed);
            var processingDocs = docs.Count(d => d.State is DocumentState.Queued or DocumentState.Processing or DocumentState.Uploaded);
            var chatMessageCount = chatHistory.Count;

            DateTimeOffset? lastActivity = null;
            if (chatHistory.Count > 0)
                lastActivity = chatHistory.Max(m => m.MessageTimestamp);

            var status = ComputeStatus(totalDocs, indexedDocs, failedDocs, processingDocs);

            rows.Add(new ProjectOperationalRow(
                project.Id, project.Name, project.Description,
                project.WorkspaceId, workspaceName,
                tenantId, tenantName,
                totalDocs, indexedDocs, failedDocs, processingDocs,
                chatMessageCount, lastActivity, status));
        }

        // Apply sorting
        Rows = SortBy switch
        {
            "activity" => rows.OrderByDescending(r => r.LastActivity ?? DateTimeOffset.MinValue).ToList(),
            "docs" => rows.OrderByDescending(r => r.TotalDocs).ToList(),
            "chat" => rows.OrderByDescending(r => r.ChatMessages).ToList(),
            "status" => rows.OrderBy(r => r.Status).ThenBy(r => r.Name).ToList(),
            _ => rows.OrderBy(r => r.Name).ToList()
        };
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var project = await _projects.GetByIdAsync(id, HttpContext.RequestAborted);
        if (project is null)
            return RedirectToPage("/Projects/Index");

        var workspace = await _workspaces.GetByIdAsync(project.WorkspaceId, HttpContext.RequestAborted);
        if (workspace is null)
            return RedirectToPage("/Projects/Index");

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);

        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();
        if (!scope.CanManageProjects(workspace.TenantId))
            return Forbid();

        await _projects.DeleteAsync(id, HttpContext.RequestAborted);
        return RedirectToPage("/Projects/Index");
    }

    private static string ComputeStatus(int totalDocs, int indexedDocs, int failedDocs, int processingDocs)
    {
        if (totalDocs == 0) return "Empty";
        if (failedDocs > 0) return "Has failures";
        if (processingDocs > 0) return "Processing";
        if (indexedDocs == totalDocs) return "All indexed";
        return "Partial";
    }

    public static string FormatTimeAgo(DateTimeOffset? timestamp)
    {
        if (timestamp is null) return "-";
        var elapsed = DateTimeOffset.UtcNow - timestamp.Value;
        if (elapsed.TotalMinutes < 1) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 30) return $"{(int)elapsed.TotalDays}d ago";
        return timestamp.Value.ToString("yyyy-MM-dd");
    }
}
