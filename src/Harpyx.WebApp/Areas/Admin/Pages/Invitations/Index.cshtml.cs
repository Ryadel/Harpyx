using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Areas.Admin.Pages.Invitations;

public class IndexModel : PageModel
{
    private readonly IUserInvitationService _invitations;
    private readonly IUserService _users;
    private readonly ITenantService _tenants;
    private readonly ITenantScopeService _tenantScope;

    public IndexModel(IUserInvitationService invitations, IUserService users, ITenantService tenants, ITenantScopeService tenantScope)
    {
        _invitations = invitations;
        _users = users;
        _tenants = tenants;
        _tenantScope = tenantScope;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public UserInvitationScope Scope { get; set; } = UserInvitationScope.SelfRegistration;

    [BindProperty]
    public Guid? TenantId { get; set; }

    [BindProperty]
    public int ExpiresInDays { get; set; } = 7;

    [BindProperty]
    public Guid RevokeInvitationId { get; set; }

    public IReadOnlyList<UserInvitationDto> Invitations { get; private set; } = Array.Empty<UserInvitationDto>();
    public IEnumerable<SelectListItem> ScopeOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> TenantOptions { get; private set; } = Array.Empty<SelectListItem>();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostInviteAsync()
    {
        try
        {
            var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
            if (Scope == UserInvitationScope.TenantMembership)
            {
                if (TenantId is null || !scope.IsTenantInScope(TenantId.Value))
                    return Forbid();
            }

            var userId = await _users.ResolveUserIdAsync(
                User.GetObjectId(),
                User.GetSubjectId(),
                User.GetEmail(),
                HttpContext.RequestAborted);

            if (userId is null)
                return Unauthorized();

            var registrationUrl = Url.Page("/Index", null, values: null, protocol: Request.Scheme);
            await _invitations.InviteAsync(
                userId.Value,
                new InviteUserRequest(Email, Scope, TenantId, ExpiresInDays, registrationUrl),
                HttpContext.RequestAborted);

            SuccessMessage = "Invitation sent.";
            return RedirectToPage("/Invitations/Index", new { area = "Admin" });
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            ErrorMessage = ex.Message;
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRevokeAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        var invitations = await _invitations.GetAllAsync(HttpContext.RequestAborted);
        var invitation = invitations.FirstOrDefault(x => x.Id == RevokeInvitationId);
        if (invitation is null)
            return RedirectToPage("/Invitations/Index", new { area = "Admin" });

        if (!scope.IsAllTenants)
        {
            if (invitation.TenantId is not Guid tenantId || !scope.IsTenantInScope(tenantId))
                return Forbid();
        }

        await _invitations.RevokeAsync(RevokeInvitationId, HttpContext.RequestAborted);
        SuccessMessage = "Invitation revoked.";
        return RedirectToPage("/Invitations/Index", new { area = "Admin" });
    }

    private async Task LoadAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        var invitations = await _invitations.GetAllAsync(HttpContext.RequestAborted);
        Invitations = scope.IsAllTenants
            ? invitations
            : invitations
                .Where(i => i.TenantId is Guid tenantId && scope.IsTenantInScope(tenantId))
                .ToList();

        ScopeOptions = Enum.GetValues<UserInvitationScope>()
            .Select(scope => new SelectListItem(scope.ToString(), scope.ToString(), scope == Scope));

        var tenants = await _tenants.GetAsync(
            new Harpyx.Application.Filters.TenantFilter { Ids = scope.EffectiveTenantIds },
            HttpContext.RequestAborted);
        TenantOptions = tenants
            .Select(t => new SelectListItem(t.Name, t.Id.ToString(), TenantId == t.Id))
            .ToList();
    }
}
