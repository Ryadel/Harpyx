using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harpyx.WebApp.Pages.Tenants;

public class IndexModel : PageModel
{
    private readonly ITenantService _tenants;
    private readonly IUserService _users;
    private readonly ITenantScopeService _tenantScope;

    public IndexModel(ITenantService tenants, IUserService users, ITenantScopeService tenantScope)
    {
        _tenants = tenants;
        _users = users;
        _tenantScope = tenantScope;
    }

    public IReadOnlyList<TenantDto> Tenants { get; private set; } = Array.Empty<TenantDto>();
    public IReadOnlyDictionary<Guid, string> OwnerEmailByTenantId { get; private set; } = new Dictionary<Guid, string>();
    public Guid? CurrentTenantId { get; private set; }
    public bool IsAllTenants { get; private set; }

    public async Task OnGetAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        CurrentTenantId = scope.CurrentTenantId;
        IsAllTenants = scope.IsAllTenants;
        var tenantIds = scope.AccessibleTenantIds;
        Tenants = await _tenants.GetAsync(new TenantFilter { Ids = tenantIds }, HttpContext.RequestAborted);

        var ownerIds = Tenants
            .Where(t => t.CreatedByUserId.HasValue)
            .Select(t => t.CreatedByUserId!.Value)
            .Distinct()
            .ToHashSet();

        if (ownerIds.Count == 0)
        {
            OwnerEmailByTenantId = Tenants.ToDictionary(t => t.Id, _ => string.Empty);
            return;
        }

        var users = await _users.GetAllAsync(HttpContext.RequestAborted);
        var ownerEmailByUserId = users
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionary(u => u.Id, u => u.Email);

        OwnerEmailByTenantId = Tenants.ToDictionary(
            t => t.Id,
            t => t.CreatedByUserId is Guid ownerId && ownerEmailByUserId.TryGetValue(ownerId, out var email)
                ? email
                : string.Empty);
    }

    public async Task<IActionResult> OnPostSetCurrentAsync(Guid id)
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.CanAccessTenant(id))
            return Forbid();

        var changed = await _tenantScope.SetCurrentTenantAsync(User, id, HttpContext.RequestAborted);
        if (!changed)
            return Forbid();

        return RedirectToPage("/Tenants/Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.CanAccessTenant(id))
            return Forbid();

        try
        {
            await _tenants.DeleteAsync(id, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException)
        {
            return Forbid();
        }

        return RedirectToPage("/Tenants/Index");
    }
}
