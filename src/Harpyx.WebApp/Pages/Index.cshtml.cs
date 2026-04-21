using Harpyx.Application.Interfaces;
using Harpyx.Application.Filters;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harpyx.WebApp.Pages;

public class IndexModel : PageModel
{
    private readonly IProjectService _projects;
    private readonly IWorkspaceService _workspaces;
    private readonly ITenantService _tenants;
    private readonly IUserService _users;
    private readonly IDocumentService _documents;
    private readonly IWebHostEnvironment _environment;
    private readonly ITenantScopeService _tenantScope;

    public IndexModel(
        IProjectService projects,
        IWorkspaceService workspaces,
        ITenantService tenants,
        IUserService users,
        IDocumentService documents,
        IWebHostEnvironment environment,
        ITenantScopeService tenantScope)
    {
        _projects = projects;
        _workspaces = workspaces;
        _tenants = tenants;
        _users = users;
        _documents = documents;
        _environment = environment;
        _tenantScope = tenantScope;
    }

    public string EnvironmentName => _environment.EnvironmentName;
    public int UserCount { get; private set; }
    public int TenantCount { get; private set; }
    public int WorkspaceCount { get; private set; }
    public int ProjectCount { get; private set; }
    public int DocumentCount { get; private set; }
    public string CurrentTenantLabel { get; private set; } = "No tenant";
    public int AccountUserCount { get; private set; }
    public int AccountTenantCount { get; private set; }
    public int CurrentTenantWorkspaceCount { get; private set; }
    public int CurrentTenantProjectCount { get; private set; }
    public int CurrentTenantDocumentCount { get; private set; }

    public async Task OnGetAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        var tenants = scope.AccessibleTenantIds.Count == 0
            ? Array.Empty<Harpyx.Application.DTOs.TenantDto>()
            : await _tenants.GetAsync(new TenantFilter { Ids = scope.AccessibleTenantIds }, HttpContext.RequestAborted);

        var tenantNamesById = tenants.ToDictionary(t => t.Id, t => t.Name);
        CurrentTenantLabel = scope.IsAllTenants
            ? "All tenants"
            : scope.CurrentTenantId is Guid currentTenantId && tenantNamesById.TryGetValue(currentTenantId, out var tenantName)
                ? tenantName
                : "No tenant";

        var allTenants = await _tenants.GetAsync(new TenantFilter(), HttpContext.RequestAborted);
        var allTenantIds = allTenants.Select(t => t.Id).ToArray();
        TenantCount = allTenants.Count;

        var allWorkspaces = await _workspaces.GetAllAsync(allTenantIds, HttpContext.RequestAborted);
        WorkspaceCount = allWorkspaces.Count;

        var allProjects = await _projects.GetAllAsync(allTenantIds, HttpContext.RequestAborted);
        ProjectCount = allProjects.Count;

        DocumentCount = await CountDocumentsAsync(allProjects);
        UserCount = (await _users.GetAllAsync(HttpContext.RequestAborted)).Count;

        var tenantScopedIds = scope.CurrentTenantId is Guid tenantScopedId
            ? new[] { tenantScopedId }
            : scope.EffectiveTenantIds;

        AccountTenantCount = tenantScopedIds.Count;
        AccountUserCount = await CountDistinctUsersAsync(tenantScopedIds);

        var tenantWorkspaces = await _workspaces.GetAllAsync(tenantScopedIds, HttpContext.RequestAborted);
        CurrentTenantWorkspaceCount = tenantWorkspaces.Count;

        var tenantProjects = await _projects.GetAllAsync(tenantScopedIds, HttpContext.RequestAborted);
        CurrentTenantProjectCount = tenantProjects.Count;
        CurrentTenantDocumentCount = await CountDocumentsAsync(tenantProjects);
    }

    private async Task<int> CountDocumentsAsync(IReadOnlyList<Harpyx.Application.DTOs.ProjectDto> projects)
    {
        var total = 0;
        foreach (var project in projects)
        {
            var docs = await _documents.GetByProjectAsync(project.Id, HttpContext.RequestAborted);
            total += docs.Count;
        }

        return total;
    }

    private async Task<int> CountDistinctUsersAsync(IReadOnlyList<Guid> tenantIds)
    {
        if (tenantIds.Count == 0)
            return 0;

        var userIds = new HashSet<Guid>();
        foreach (var tenantId in tenantIds)
        {
            var users = await _users.GetByTenantIdAsync(tenantId, HttpContext.RequestAborted);
            foreach (var user in users)
            {
                userIds.Add(user.Id);
            }
        }

        return userIds.Count;
    }
}
