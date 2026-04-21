using Harpyx.Application.DTOs;
using Harpyx.Application.Exceptions;
using Harpyx.Application.Interfaces;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Areas.Admin.Pages.Tenants;

public class EditModel : PageModel
{
    private readonly ITenantService _tenants;
    private readonly IUserService _users;
    private readonly ITenantScopeService _tenantScope;

    public EditModel(ITenantService tenants, IUserService users, ITenantScopeService tenantScope)
    {
        _tenants = tenants;
        _users = users;
        _tenantScope = tenantScope;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public bool IsActive { get; set; } = true;

    [BindProperty]
    public bool IsVisibleToAllUsers { get; set; }

    [BindProperty]
    public Guid? CreatedByUserId { get; set; }

    public IEnumerable<SelectListItem> OwnerOptions { get; private set; } = Array.Empty<SelectListItem>();
    public string? ErrorMessage { get; private set; }
    public string Title => Id is null ? "Add Tenant" : "Edit Tenant";

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadOwnerOptionsAsync();

        if (Id is null)
        {
            if (CreatedByUserId is null)
            {
                CreatedByUserId = await _users.ResolveUserIdAsync(
                    User.GetObjectId(),
                    User.GetSubjectId(),
                    User.GetEmail(),
                    HttpContext.RequestAborted);
            }
            return Page();
        }

        var tenant = await _tenants.GetByIdAsync(Id.Value, HttpContext.RequestAborted);
        if (tenant is null)
        {
            return RedirectToPage("/Tenants/Index", new { area = "Admin" });
        }

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.IsTenantInScope(tenant.Id))
            return Forbid();

        Name = tenant.Name;
        IsActive = tenant.IsActive;
        IsVisibleToAllUsers = tenant.IsVisibleToAllUsers;
        CreatedByUserId = tenant.CreatedByUserId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadOwnerOptionsAsync();

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (Id is null && !scope.IsAllTenants)
            return Forbid();

        if (Id is Guid tenantId && !scope.IsTenantInScope(tenantId))
            return Forbid();

        try
        {
            if (Id is null)
            {
                await _tenants.CreateAsync(
                    new CreateTenantRequest(
                        CreatedByUserId,
                        Name,
                        IsActive,
                        IsVisibleToAllUsers),
                    HttpContext.RequestAborted);
            }
            else
            {
                await _tenants.UpdateAsync(
                    new TenantDto(
                        Id.Value,
                        Name,
                        IsActive,
                        IsVisibleToAllUsers,
                        CreatedByUserId: CreatedByUserId),
                    HttpContext.RequestAborted);
            }

            return RedirectToPage("/Tenants/Index", new { area = "Admin" });
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UsageLimitExceededException)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private async Task LoadOwnerOptionsAsync()
    {
        var users = await _users.GetAllAsync(HttpContext.RequestAborted);
        OwnerOptions = users
            .OrderBy(u => u.Email)
            .Select(u => new SelectListItem(u.Email, u.Id.ToString(), CreatedByUserId == u.Id))
            .ToList();
    }
}
