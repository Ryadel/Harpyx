using Harpyx.Application.DTOs;
using Harpyx.Application.Exceptions;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Developer.Api;

public class EditModel : PageModel
{
    private readonly IApiKeyService _apiKeys;
    private readonly IUserService _users;
    private readonly IUsageLimitService _usageLimits;

    public EditModel(
        IApiKeyService apiKeys,
        IUserService users,
        IUsageLimitService usageLimits)
    {
        _apiKeys = apiKeys;
        _users = users;
        _usageLimits = usageLimits;
    }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public DateTime? ExpiresAtUtcDate { get; set; }

    [BindProperty]
    public bool PermissionQueryProjects { get; set; } = true;

    [BindProperty]
    public bool PermissionUploadDocuments { get; set; }

    [BindProperty]
    public bool PermissionDeleteDocuments { get; set; }

    [BindProperty]
    public bool PermissionCreateProjects { get; set; }

    [BindProperty]
    public bool PermissionCreateWorkspaces { get; set; }

    [BindProperty]
    public bool PermissionDeleteProjects { get; set; }

    [BindProperty]
    public bool PermissionDeleteWorkspaces { get; set; }

    public bool IsApiEnabledForInstance { get; private set; }

    public ApiKeyCreateResultDto? CreatedApiKey { get; private set; }

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return RedirectToPage("/Index");

        IsApiEnabledForInstance = await _usageLimits.IsApiEnabledForUserAsync(userId.Value, HttpContext.RequestAborted);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return RedirectToPage("/Index");

        IsApiEnabledForInstance = await _usageLimits.IsApiEnabledForUserAsync(userId.Value, HttpContext.RequestAborted);
        if (!IsApiEnabledForInstance)
        {
            ErrorMessage = "API access is disabled for this instance.";
            return Page();
        }

        var normalizedName = (Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            ModelState.AddModelError(nameof(Name), "Key name is required.");
            return Page();
        }

        if (normalizedName.Length > 200)
        {
            ModelState.AddModelError(nameof(Name), "Key name must be at most 200 characters.");
            return Page();
        }

        var permissions = BuildPermissions();
        if (permissions == ApiPermission.None)
        {
            ModelState.AddModelError(string.Empty, "Select at least one permission.");
            return Page();
        }

        DateTimeOffset? expiresAtUtc = null;
        if (ExpiresAtUtcDate is DateTime expiresDate)
        {
            if (expiresDate.Date <= DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(ExpiresAtUtcDate), "Expiration date must be in the future.");
                return Page();
            }

            expiresAtUtc = new DateTimeOffset(DateTime.SpecifyKind(expiresDate.Date, DateTimeKind.Utc));
        }

        try
        {
            CreatedApiKey = await _apiKeys.CreateAsync(
                userId.Value,
                new ApiKeyCreateRequest(normalizedName, expiresAtUtc, permissions),
                HttpContext.RequestAborted);
            SuccessMessage = "API key created. Copy it now: for security reasons it is shown only once.";

            Name = string.Empty;
            ExpiresAtUtcDate = null;
            PermissionQueryProjects = true;
            PermissionUploadDocuments = false;
            PermissionDeleteDocuments = false;
            PermissionCreateProjects = false;
            PermissionCreateWorkspaces = false;
            PermissionDeleteProjects = false;
            PermissionDeleteWorkspaces = false;
            return Page();
        }
        catch (Exception ex) when (ex is ArgumentException or UsageLimitExceededException)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private ApiPermission BuildPermissions()
    {
        var permissions = ApiPermission.None;
        if (PermissionQueryProjects) permissions |= ApiPermission.QueryProjects;
        if (PermissionUploadDocuments) permissions |= ApiPermission.UploadDocuments;
        if (PermissionDeleteDocuments) permissions |= ApiPermission.DeleteDocuments;
        if (PermissionCreateProjects) permissions |= ApiPermission.CreateProjects;
        if (PermissionCreateWorkspaces) permissions |= ApiPermission.CreateWorkspaces;
        if (PermissionDeleteProjects) permissions |= ApiPermission.DeleteProjects;
        if (PermissionDeleteWorkspaces) permissions |= ApiPermission.DeleteWorkspaces;
        return permissions;
    }

    private async Task<Guid?> ResolveUserIdAsync()
    {
        return await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            HttpContext.RequestAborted);
    }
}
