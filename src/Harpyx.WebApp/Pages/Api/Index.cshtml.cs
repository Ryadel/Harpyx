using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Api;

public class IndexModel : PageModel
{
    private readonly IApiKeyService _apiKeys;
    private readonly IUserService _users;
    private readonly IUsageLimitService _usageLimits;

    public IndexModel(
        IApiKeyService apiKeys,
        IUserService users,
        IUsageLimitService usageLimits)
    {
        _apiKeys = apiKeys;
        _users = users;
        _usageLimits = usageLimits;
    }

    [BindProperty]
    public Guid RevokeApiKeyId { get; set; }

    [BindProperty]
    public Guid DeleteApiKeyId { get; set; }

    public IReadOnlyList<ApiKeyDto> ApiKeys { get; private set; } = Array.Empty<ApiKeyDto>();
    public bool IsApiEnabledForInstance { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return RedirectToPage("/Index");

        await LoadAsync(userId.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return RedirectToPage("/Index");

        if (RevokeApiKeyId == Guid.Empty)
        {
            ErrorMessage = "Invalid API key.";
            return RedirectToPage("/Api/Index");
        }

        var revoked = await _apiKeys.RevokeAsync(userId.Value, RevokeApiKeyId, HttpContext.RequestAborted);
        SuccessMessage = revoked ? "API key revoked." : "API key not found.";
        return RedirectToPage("/Api/Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return RedirectToPage("/Index");

        if (DeleteApiKeyId == Guid.Empty)
        {
            ErrorMessage = "Invalid API key.";
            return RedirectToPage("/Api/Index");
        }

        var deleted = await _apiKeys.DeleteAsync(userId.Value, DeleteApiKeyId, HttpContext.RequestAborted);
        SuccessMessage = deleted
            ? "API key deleted."
            : "API key cannot be deleted. Revoke it first.";
        return RedirectToPage("/Api/Index");
    }

    public static IReadOnlyList<string> DescribePermissions(ApiPermission permissions)
    {
        var labels = new List<string>();
        if (permissions.HasFlag(ApiPermission.QueryProjects)) labels.Add("Query");
        if (permissions.HasFlag(ApiPermission.UploadDocuments)) labels.Add("Upload docs");
        if (permissions.HasFlag(ApiPermission.DeleteDocuments)) labels.Add("Delete docs");
        if (permissions.HasFlag(ApiPermission.CreateProjects)) labels.Add("Create projects");
        if (permissions.HasFlag(ApiPermission.CreateWorkspaces)) labels.Add("Create workspaces");
        if (permissions.HasFlag(ApiPermission.DeleteProjects)) labels.Add("Delete projects");
        if (permissions.HasFlag(ApiPermission.DeleteWorkspaces)) labels.Add("Delete workspaces");
        return labels;
    }

    private async Task<Guid?> ResolveUserIdAsync()
    {
        return await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            HttpContext.RequestAborted);
    }

    private async Task LoadAsync(Guid userId)
    {
        IsApiEnabledForInstance = await _usageLimits.IsApiEnabledForUserAsync(userId, HttpContext.RequestAborted);
        ApiKeys = await _apiKeys.GetAllAsync(userId, HttpContext.RequestAborted);
    }
}
