using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Members;

public class IndexModel : PageModel
{
    private readonly ITenantScopeService _tenantScope;
    private readonly IUserService _users;
    private readonly ITenantMembershipService _memberships;
    private readonly ITenantService _tenants;

    public IndexModel(
        ITenantScopeService tenantScope,
        IUserService users,
        ITenantMembershipService memberships,
        ITenantService tenants)
    {
        _tenantScope = tenantScope;
        _users = users;
        _memberships = memberships;
        _tenants = tenants;
    }

    public IReadOnlyList<TenantMemberDto> Members { get; private set; } = Array.Empty<TenantMemberDto>();
    public IEnumerable<SelectListItem> RoleOptions { get; private set; } = Array.Empty<SelectListItem>();
    public Guid? CurrentTenantId { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public string CurrentTenantLabel { get; private set; } = "No tenant";
    public bool CanManage { get; private set; }
    public bool IsCurrentTenantPersonalOwnedByCurrentUser { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPageAsync();
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid userId, TenantRole tenantRole)
    {
        var context = await LoadPageAsync();
        if (!context.IsReady)
            return Page();

        if (!CanManage)
            return Forbid();

        try
        {
            await _memberships.SaveAsync(
                context.ActorUserId!.Value,
                context.TenantId!.Value,
                new SaveTenantMembershipRequest(userId, tenantRole),
                context.Scope!.IsAdmin,
                HttpContext.RequestAborted);
            SuccessMessage = "Member permissions updated.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }

        return RedirectToPage("/Members/Index");
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid userId)
    {
        var context = await LoadPageAsync();
        if (!context.IsReady)
            return Page();

        if (!CanManage)
            return Forbid();

        try
        {
            await _memberships.RevokeAsync(
                context.ActorUserId!.Value,
                context.TenantId!.Value,
                userId,
                context.Scope!.IsAdmin,
                HttpContext.RequestAborted);
            SuccessMessage = "Member removed.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }

        return RedirectToPage("/Members/Index");
    }

    private async Task<LoadContext> LoadPageAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        var actorUserId = await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            HttpContext.RequestAborted);
        CurrentUserId = actorUserId;

        RoleOptions = Enum.GetValues<TenantRole>()
            .Select(role => new SelectListItem(role.ToString(), role.ToString()))
            .ToList();

        if (scope.IsAllTenants)
        {
            ErrorMessage = "Select a tenant to manage members.";
            CurrentTenantLabel = "All tenants";
            CanManage = false;
            Members = Array.Empty<TenantMemberDto>();
            return new LoadContext(false, null, null, scope);
        }

        if (scope.CurrentTenantId is not Guid tenantId)
        {
            ErrorMessage = "No current tenant selected.";
            CurrentTenantLabel = "No tenant";
            CanManage = false;
            Members = Array.Empty<TenantMemberDto>();
            return new LoadContext(false, null, actorUserId, scope);
        }

        CurrentTenantId = tenantId;
        var tenant = await _tenants.GetByIdAsync(tenantId, HttpContext.RequestAborted);
        CurrentTenantLabel = tenant?.Name ?? "Unknown tenant";
        IsCurrentTenantPersonalOwnedByCurrentUser =
            tenant?.IsPersonal == true &&
            actorUserId is Guid currentUserId &&
            tenant.CreatedByUserId == currentUserId;
        CanManage = scope.CanManageMembers(tenantId);
        ErrorMessage = null;

        Members = await _memberships.GetMembersAsync(tenantId, HttpContext.RequestAborted);

        return new LoadContext(true, tenantId, actorUserId, scope);
    }

    private sealed record LoadContext(bool IsReady, Guid? TenantId, Guid? ActorUserId, TenantScopeContext? Scope);
}
