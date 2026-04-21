using Harpyx.Application.DTOs;
using Harpyx.Application.Filters;
using Harpyx.Application.Interfaces;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harpyx.WebApp.Pages.Workspaces;

public class IndexModel : PageModel
{
    private readonly IWorkspaceService _workspaces;
    private readonly IProjectService _projects;
    private readonly IDocumentService _documents;
    private readonly ITenantScopeService _tenantScope;
    private readonly ITenantService _tenants;

    public IndexModel(
        IWorkspaceService workspaces,
        IProjectService projects,
        IDocumentService documents,
        ITenantScopeService tenantScope,
        ITenantService tenants)
    {
        _workspaces = workspaces;
        _projects = projects;
        _documents = documents;
        _tenantScope = tenantScope;
        _tenants = tenants;
    }

    public IReadOnlyList<WorkspaceDto> Workspaces { get; private set; } = Array.Empty<WorkspaceDto>();
    public IReadOnlyDictionary<Guid, string> TenantNamesById { get; private set; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, int> WorkspaceProjectCountsById { get; private set; } = new Dictionary<Guid, int>();
    public IReadOnlyDictionary<Guid, int> WorkspaceDocumentCountsById { get; private set; } = new Dictionary<Guid, int>();
    public IReadOnlyDictionary<Guid, IReadOnlyList<ProjectDto>> ProjectsByWorkspaceId { get; private set; }
        = new Dictionary<Guid, IReadOnlyList<ProjectDto>>();
    public IReadOnlyDictionary<Guid, int> ProjectDocumentCountsById { get; private set; }
        = new Dictionary<Guid, int>();

    public async Task OnGetAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);

        Workspaces = await _workspaces.GetAllAsync(scope.EffectiveTenantIds, HttpContext.RequestAborted);
        var projects = await _projects.GetAllAsync(scope.EffectiveTenantIds, HttpContext.RequestAborted);

        WorkspaceProjectCountsById = projects
            .GroupBy(p => p.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.Count());

        ProjectsByWorkspaceId = projects
            .GroupBy(p => p.WorkspaceId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProjectDto>)g.ToList());

        var documentTasks = projects
            .Select(p => _documents.GetByProjectAsync(p.Id, HttpContext.RequestAborted))
            .ToArray();
        var documentsByProject = await Task.WhenAll(documentTasks);

        var workspaceDocumentCounts = new Dictionary<Guid, int>();
        for (var i = 0; i < projects.Count; i++)
        {
            var workspaceId = projects[i].WorkspaceId;
            var documentCount = documentsByProject[i].Count;

            if (workspaceDocumentCounts.TryGetValue(workspaceId, out var current))
                workspaceDocumentCounts[workspaceId] = current + documentCount;
            else
                workspaceDocumentCounts[workspaceId] = documentCount;
        }
        WorkspaceDocumentCountsById = workspaceDocumentCounts;

        ProjectDocumentCountsById = projects
            .Select((project, index) => new { project.Id, Count = documentsByProject[index].Count })
            .ToDictionary(x => x.Id, x => x.Count);

        var tenantDtos = await _tenants.GetAsync(
            new TenantFilter { Ids = scope.EffectiveTenantIds, IncludeVisibleToAllUsers = true },
            HttpContext.RequestAborted);

        TenantNamesById = tenantDtos
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);
    }

    public IReadOnlyList<ProjectDto> GetProjectsForWorkspace(Guid workspaceId)
        => ProjectsByWorkspaceId.TryGetValue(workspaceId, out var list) ? list : Array.Empty<ProjectDto>();

    public int GetProjectDocumentCount(Guid projectId)
        => ProjectDocumentCountsById.TryGetValue(projectId, out var count) ? count : 0;

    public async Task<IActionResult> OnPostDeleteProjectAsync(Guid id)
    {
        var project = await _projects.GetByIdAsync(id, HttpContext.RequestAborted);
        if (project is null)
            return RedirectToPage("/Workspaces/Index");

        var workspace = await _workspaces.GetByIdAsync(project.WorkspaceId, HttpContext.RequestAborted);
        if (workspace is null)
            return RedirectToPage("/Workspaces/Index");

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);

        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();
        if (!scope.CanManageProjects(workspace.TenantId))
            return Forbid();

        await _projects.DeleteAsync(id, HttpContext.RequestAborted);
        return RedirectToPage("/Workspaces/Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var workspace = await _workspaces.GetByIdAsync(id, HttpContext.RequestAborted);
        if (workspace is null)
            return RedirectToPage("/Workspaces/Index");

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);

        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();
        if (!scope.CanManageWorkspaces(workspace.TenantId))
            return Forbid();

        await _workspaces.DeleteAsync(id, HttpContext.RequestAborted);
        return RedirectToPage("/Workspaces/Index");
    }
}
