using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IUserService _users;
    private readonly ITenantScopeService _tenantScope;

    public IndexModel(IUserService users, ITenantScopeService tenantScope)
    {
        _users = users;
        _tenantScope = tenantScope;
    }

    public IReadOnlyList<UserDto> Users { get; private set; } = Array.Empty<UserDto>();
    [TempData]
    public string? SuccessMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (scope.IsAllTenants)
        {
            Users = await _users.GetAllAsync(HttpContext.RequestAborted);
            return;
        }

        if (scope.CurrentTenantId is not Guid currentTenantId)
        {
            Users = Array.Empty<UserDto>();
            return;
        }

        Users = await _users.GetByTenantIdAsync(currentTenantId, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.IsAllTenants && scope.CurrentTenantId is Guid currentTenantId)
        {
            var scopedUsers = await _users.GetByTenantIdAsync(currentTenantId, HttpContext.RequestAborted);
            if (scopedUsers.All(u => u.Id != id))
                return Forbid();
        }

        var currentUserId = await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            HttpContext.RequestAborted);

        try
        {
            await _users.DeleteAsync(id, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage("/Users/Index");
        }

        if (currentUserId is not null && currentUserId.Value == id)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Index");
        }

        SuccessMessage = "User deleted successfully.";
        return RedirectToPage("/Users/Index");
    }
}
