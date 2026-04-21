using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Controllers;

[ApiController]
[Route("api/workspaces")]
[IgnoreAntiforgeryToken]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaces;
    private readonly IUserService _users;
    private readonly ITenantScopeService _tenantScope;

    public WorkspacesController(
        IWorkspaceService workspaces,
        IUserService users,
        ITenantScopeService tenantScope)
    {
        _workspaces = workspaces;
        _users = users;
        _tenantScope = tenantScope;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceDto>>> GetAll([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.QueryProjects))
            return Forbid();

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        if (scope.EffectiveTenantIds.Count == 0)
            return Ok(Array.Empty<WorkspaceDto>());

        IReadOnlyList<Guid> effectiveTenantIds = scope.EffectiveTenantIds;
        if (tenantId is Guid requestedTenantId)
        {
            if (!scope.CanAccessTenant(requestedTenantId))
                return Forbid();

            if (!scope.IsAllTenants && scope.CurrentTenantId != requestedTenantId)
                return Forbid();

            effectiveTenantIds = new[] { requestedTenantId };
        }

        var workspaces = await _workspaces.GetAllAsync(effectiveTenantIds, cancellationToken);
        return Ok(workspaces);
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceDto>> Create([FromBody] CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.CreateWorkspaces))
            return Forbid();

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        if (!scope.IsTenantInScope(request.TenantId))
            return Forbid();
        if (!scope.CanManageWorkspaces(request.TenantId))
            return Forbid();

        var userId = await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            cancellationToken);
        if (userId is null)
            return Forbid();

        var safeRequest = request with { CreatedByUserId = userId.Value };
        var workspace = await _workspaces.CreateAsync(safeRequest, cancellationToken);
        return Ok(workspace);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkspaceDto>> Update(Guid id, [FromBody] WorkspaceDto workspace, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.CreateWorkspaces))
            return Forbid();

        if (id != workspace.Id)
            return BadRequest();

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();
        if (!scope.CanManageWorkspaces(workspace.TenantId))
            return Forbid();

        var existing = await _workspaces.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return NotFound(new { error = "Workspace not found." });

        if (!scope.IsTenantInScope(existing.TenantId))
            return Forbid();
        if (!scope.CanManageWorkspaces(existing.TenantId))
            return Forbid();

        var updated = await _workspaces.UpdateAsync(workspace, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.DeleteWorkspaces))
            return Forbid();

        var existing = await _workspaces.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return NotFound(new { error = "Workspace not found." });

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        if (!scope.IsTenantInScope(existing.TenantId))
            return Forbid();
        if (!scope.CanManageWorkspaces(existing.TenantId))
            return Forbid();

        await _workspaces.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
