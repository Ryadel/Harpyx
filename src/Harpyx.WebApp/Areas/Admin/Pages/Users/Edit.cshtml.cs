using Harpyx.Application.DTOs;
using Harpyx.Application.Filters;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Harpyx.WebApp.Areas.Admin.Pages.Users;

public class EditModel : PageModel
{
    private readonly IUserService _users;
    private readonly ITenantService _tenants;
    private readonly ITenantScopeService _tenantScope;

    public EditModel(IUserService users, ITenantService tenants, ITenantScopeService tenantScope)
    {
        _users = users;
        _tenants = tenants;
        _tenantScope = tenantScope;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string? ObjectId { get; set; } = string.Empty;

    [BindProperty]
    public string? SubjectId { get; set; } = string.Empty;

    [BindProperty]
    public UserRole Role { get; set; } = UserRole.ReadOnly;

    [BindProperty]
    public bool IsActive { get; set; } = true;

    [BindProperty]
    public List<Guid> SelectedTenantIds { get; set; } = new();

    public IEnumerable<SelectListItem> RoleOptions => Enum.GetValues<UserRole>()
        .Select(role => new SelectListItem(role.ToString(), role.ToString(), role == Role));

    public IEnumerable<SelectListItem> TenantOptions { get; private set; } = Array.Empty<SelectListItem>();

    public string Title => Id is null ? "Add User" : "Edit User";

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadTenantOptionsAsync();

        if (Id is null)
        {
            return Page();
        }

        var user = await _users.GetForEditAsync(Id.Value, HttpContext.RequestAborted);
        if (user is null)
        {
            return RedirectToPage("/Users/Index", new { area = "Admin" });
        }

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.IsAllTenants && scope.CurrentTenantId is Guid currentTenantId && !user.TenantIds.Contains(currentTenantId))
            return Forbid();

        Email = user.Email;
        ObjectId = user.ObjectId;
        SubjectId = user.SubjectId;
        Role = user.Role;
        IsActive = user.IsActive;
        SelectedTenantIds = user.TenantIds.ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadTenantOptionsAsync();
        var allowedTenantIds = await GetAllowedTenantIdsAsync();
        if (SelectedTenantIds.Any(id => !allowedTenantIds.Contains(id)))
            return Forbid();

        await _users.SaveAsync(
            new UserSaveRequest(
                Id,
                ObjectId,
                SubjectId,
                Email,
                Role,
                IsActive,
                SelectedTenantIds),
            HttpContext.RequestAborted);

        return RedirectToPage("/Users/Index", new { area = "Admin" });
    }

    private async Task LoadTenantOptionsAsync()
    {
        var allowedTenantIds = await GetAllowedTenantIdsAsync();
        var tenants = await _tenants.GetAsync(new TenantFilter { Ids = allowedTenantIds }, HttpContext.RequestAborted);
        TenantOptions = tenants.Select(t => new SelectListItem(t.Name, t.Id.ToString(), SelectedTenantIds.Contains(t.Id))).ToList();
    }

    private async Task<IReadOnlyList<Guid>> GetAllowedTenantIdsAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        return scope.EffectiveTenantIds;
    }
}
