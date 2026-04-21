using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Controllers;

[ApiController]
[Route("api/documents")]
[IgnoreAntiforgeryToken]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;
    private readonly IProjectService _projects;
    private readonly IWorkspaceService _workspaces;
    private readonly IUserService _users;
    private readonly ITenantScopeService _tenantScope;

    public DocumentsController(
        IDocumentService documents,
        IProjectService projects,
        IWorkspaceService workspaces,
        IUserService users,
        ITenantScopeService tenantScope)
    {
        _documents = documents;
        _projects = projects;
        _workspaces = workspaces;
        _users = users;
        _tenantScope = tenantScope;
    }

    [HttpGet("project/{projectId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> GetByProject(Guid projectId, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.QueryProjects))
            return Forbid();

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return NotFound(new { error = "Project not found." });

        var workspace = await _workspaces.GetByIdAsync(project.WorkspaceId, cancellationToken);
        if (workspace is null)
            return NotFound(new { error = "Workspace not found." });

        var scope = await _tenantScope.GetScopeAsync(User, cancellationToken);
        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();

        var documents = await _documents.GetByProjectAsync(projectId, cancellationToken);
        return Ok(documents);
    }

    [HttpPost("project/{projectId:guid}/upload")]
    [RequestSizeLimit(1024L * 1024L * 1024L)]
    public async Task<ActionResult<DocumentDto>> Upload(Guid projectId, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.UploadDocuments))
            return Forbid();

        if (file is null || file.Length <= 0)
            return BadRequest(new { error = "A non-empty file is required." });

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
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

        var userId = await ResolveCurrentUserIdAsync(cancellationToken);
        if (userId is null)
            return Unauthorized();

        await using var stream = file.OpenReadStream();
        try
        {
            var document = await _documents.UploadAsync(
                new UploadDocumentRequest(
                    userId.Value,
                    projectId,
                    file.FileName,
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    stream,
                    file.Length),
                cancellationToken);

            return Ok(document);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!User.HasApiPermission(ApiPermission.DeleteDocuments))
            return Forbid();

        var document = await _documents.GetByIdAsync(id, cancellationToken);
        if (document is null)
            return NotFound(new { error = "Document not found." });

        var project = await _projects.GetByIdAsync(document.ProjectId, cancellationToken);
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

        await _documents.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private async Task<Guid?> ResolveCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        return await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            cancellationToken);
    }
}
