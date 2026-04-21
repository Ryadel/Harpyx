using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harpyx.WebApp.Areas.Admin.Pages.UsageLimits;

public record LimitCellModel(string ValueFieldName, int? Value, string UnlimitedFieldName, bool Unlimited);

public class IndexModel : PageModel
{
    private readonly IUsageLimitService _usageLimits;

    public IndexModel(IUsageLimitService usageLimits)
    {
        _usageLimits = usageLimits;
    }

    [BindProperty]
    public UsageLimitInput Limits { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        Limits = FromDto(await _usageLimits.GetAsync(HttpContext.RequestAborted));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            await _usageLimits.SaveAsync(ToRequest(Limits), HttpContext.RequestAborted);
            SuccessMessage = "Usage limits saved.";
            return RedirectToPage("/UsageLimits/Index", new { area = "Admin" });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private static UsageLimitInput FromDto(UsageLimitsDto dto)
    {
        return new UsageLimitInput
        {
            TenantsPerUser = dto.TenantsPerUser,
            TenantsPerUserUnlimited = dto.TenantsPerUser is null,
            WorkspacesPerUser = dto.WorkspacesPerUser,
            WorkspacesPerUserUnlimited = dto.WorkspacesPerUser is null,
            DocumentsPerWorkspace = dto.DocumentsPerWorkspace,
            DocumentsPerWorkspaceUnlimited = dto.DocumentsPerWorkspace is null,
            StoragePerUserGb = dto.StoragePerUserGb,
            StoragePerUserGbUnlimited = dto.StoragePerUserGb is null,
            StoragePerTenantGb = dto.StoragePerTenantGb,
            StoragePerTenantGbUnlimited = dto.StoragePerTenantGb is null,
            StoragePerWorkspaceGb = dto.StoragePerWorkspaceGb,
            StoragePerWorkspaceGbUnlimited = dto.StoragePerWorkspaceGb is null,
            ProjectsPerWorkspace = dto.ProjectsPerWorkspace,
            ProjectsPerWorkspaceUnlimited = dto.ProjectsPerWorkspace is null,
            PermanentProjectsPerWorkspace = dto.PermanentProjectsPerWorkspace,
            PermanentProjectsPerWorkspaceUnlimited = dto.PermanentProjectsPerWorkspace is null,
            MaxTemporaryProjectLifetimeHours = dto.MaxTemporaryProjectLifetimeHours,
            MaxTemporaryProjectLifetimeHoursUnlimited = dto.MaxTemporaryProjectLifetimeHours is null,
            LlmProvidersPerUser = dto.LlmProvidersPerUser,
            LlmProvidersPerUserUnlimited = dto.LlmProvidersPerUser is null,
            EnableOcr = dto.EnableOcr,
            EnableRagIndexing = dto.EnableRagIndexing,
            EnableApi = dto.EnableApi
        };
    }

    private static UsageLimitsSaveRequest ToRequest(UsageLimitInput input)
    {
        int? Normalize(int? value) => value is not null && value.Value < 0 ? 0 : value;

        return new UsageLimitsSaveRequest(
            input.TenantsPerUserUnlimited ? null : Normalize(input.TenantsPerUser),
            input.WorkspacesPerUserUnlimited ? null : Normalize(input.WorkspacesPerUser),
            input.DocumentsPerWorkspaceUnlimited ? null : Normalize(input.DocumentsPerWorkspace),
            input.StoragePerUserGbUnlimited ? null : Normalize(input.StoragePerUserGb),
            input.StoragePerTenantGbUnlimited ? null : Normalize(input.StoragePerTenantGb),
            input.StoragePerWorkspaceGbUnlimited ? null : Normalize(input.StoragePerWorkspaceGb),
            input.ProjectsPerWorkspaceUnlimited ? null : Normalize(input.ProjectsPerWorkspace),
            input.PermanentProjectsPerWorkspaceUnlimited ? null : Normalize(input.PermanentProjectsPerWorkspace),
            input.MaxTemporaryProjectLifetimeHoursUnlimited ? null : Normalize(input.MaxTemporaryProjectLifetimeHours),
            input.LlmProvidersPerUserUnlimited ? null : Normalize(input.LlmProvidersPerUser),
            input.EnableOcr,
            input.EnableRagIndexing,
            input.EnableApi);
    }

    public class UsageLimitInput
    {
        public int? TenantsPerUser { get; set; }
        public bool TenantsPerUserUnlimited { get; set; }

        public int? WorkspacesPerUser { get; set; }
        public bool WorkspacesPerUserUnlimited { get; set; }

        public int? DocumentsPerWorkspace { get; set; }
        public bool DocumentsPerWorkspaceUnlimited { get; set; }

        public int? StoragePerUserGb { get; set; }
        public bool StoragePerUserGbUnlimited { get; set; }

        public int? StoragePerTenantGb { get; set; }
        public bool StoragePerTenantGbUnlimited { get; set; }

        public int? StoragePerWorkspaceGb { get; set; }
        public bool StoragePerWorkspaceGbUnlimited { get; set; }

        public int? ProjectsPerWorkspace { get; set; }
        public bool ProjectsPerWorkspaceUnlimited { get; set; }

        public int? PermanentProjectsPerWorkspace { get; set; }
        public bool PermanentProjectsPerWorkspaceUnlimited { get; set; }

        public int? MaxTemporaryProjectLifetimeHours { get; set; }
        public bool MaxTemporaryProjectLifetimeHoursUnlimited { get; set; }

        public int? LlmProvidersPerUser { get; set; }
        public bool LlmProvidersPerUserUnlimited { get; set; }

        public bool EnableOcr { get; set; }
        public bool EnableRagIndexing { get; set; }
        public bool EnableApi { get; set; }
    }
}
