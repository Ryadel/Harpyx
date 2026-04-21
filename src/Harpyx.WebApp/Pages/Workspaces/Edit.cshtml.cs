using Harpyx.Application.DTOs;
using Harpyx.Application.Exceptions;
using Harpyx.Application.Filters;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Workspaces;

public class EditModel : PageModel
{
    private readonly IWorkspaceService _workspaces;
    private readonly ITenantService _tenants;
    private readonly IUserService _users;
    private readonly IUserLlmProviderService _providers;
    private readonly ITenantScopeService _tenantScope;

    public EditModel(
        IWorkspaceService workspaces,
        ITenantService tenants,
        IUserService users,
        IUserLlmProviderService providers,
        ITenantScopeService tenantScope)
    {
        _workspaces = workspaces;
        _tenants = tenants;
        _users = users;
        _providers = providers;
        _tenantScope = tenantScope;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public Guid TenantId { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public bool IsActive { get; set; } = true;

    [BindProperty]
    public bool IsChatLlmEnabled { get; set; }

    [BindProperty]
    public Guid? ChatModelId { get; set; }

    [BindProperty]
    public bool IsRagLlmEnabled { get; set; }

    [BindProperty]
    public Guid? RagEmbeddingModelId { get; set; }

    [BindProperty]
    public bool IsOcrLlmEnabled { get; set; }

    [BindProperty]
    public Guid? OcrModelId { get; set; }

    public IEnumerable<SelectListItem> TenantOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> ChatModelOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> RagProviderOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> OcrModelOptions { get; private set; } = Array.Empty<SelectListItem>();
    public bool HasChatModelOptions => ChatModelOptions.Any();
    public bool HasRagProviderOptions => RagProviderOptions.Any();
    public bool HasOcrModelOptions => OcrModelOptions.Any();
    public string? ErrorMessage { get; private set; }
    public string Title => Id is null ? "Create Workspace" : "Edit Workspace";

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadTenantOptionsAsync();

        if (Id is null)
        {
            if (TenantOptions.Any() && TenantId == Guid.Empty)
            {
                TenantId = Guid.Parse(TenantOptions.First().Value!);
            }

            if (TenantId != Guid.Empty)
            {
                await LoadProviderOptionsAsync(TenantId);
                ApplyCreateDefaultsForProviders();
            }

            return Page();
        }

        var workspace = await _workspaces.GetByIdAsync(Id.Value, HttpContext.RequestAborted);
        if (workspace is null)
            return RedirectToPage("/Workspaces/Index");

        var effectiveTenantIds = await GetEffectiveTenantIdsAsync();
        if (!effectiveTenantIds.Contains(workspace.TenantId))
            return Forbid();
        var existingScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!existingScope.CanManageWorkspaces(workspace.TenantId))
            return Forbid();

        TenantId = workspace.TenantId;
        Name = workspace.Name;
        Description = workspace.Description;
        IsActive = workspace.IsActive;
        IsChatLlmEnabled = workspace.IsChatLlmEnabled;
        ChatModelId = workspace.ChatModelId;
        IsRagLlmEnabled = workspace.IsRagLlmEnabled;
        RagEmbeddingModelId = workspace.RagEmbeddingModelId;
        IsOcrLlmEnabled = workspace.IsOcrLlmEnabled;
        OcrModelId = workspace.OcrModelId;

        await LoadProviderOptionsAsync(TenantId);
        if (!HasChatModelOptions)
        {
            IsChatLlmEnabled = false;
            ChatModelId = null;
        }

        if (!HasRagProviderOptions)
        {
            IsRagLlmEnabled = false;
            RagEmbeddingModelId = null;
        }

        if (!HasOcrModelOptions)
        {
            IsOcrLlmEnabled = false;
            OcrModelId = null;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadTenantOptionsAsync();
        if (TenantId != Guid.Empty)
            await LoadProviderOptionsAsync(TenantId);

        if (!HasChatModelOptions)
        {
            IsChatLlmEnabled = false;
            ChatModelId = null;
        }

        if (!HasRagProviderOptions)
        {
            IsRagLlmEnabled = false;
            RagEmbeddingModelId = null;
        }

        if (!HasOcrModelOptions)
        {
            IsOcrLlmEnabled = false;
            OcrModelId = null;
        }

        if (!IsChatLlmEnabled)
            ChatModelId = null;
        if (!IsRagLlmEnabled)
            RagEmbeddingModelId = null;
        if (!IsOcrLlmEnabled)
            OcrModelId = null;

        var effectiveTenantIds = await GetEffectiveTenantIdsAsync();
        if (!effectiveTenantIds.Contains(TenantId))
            return Forbid();

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.CanManageWorkspaces(TenantId))
            return Forbid();

        if (Id is null)
        {
            var userId = await _users.ResolveUserIdAsync(
                User.GetObjectId(),
                User.GetSubjectId(),
                User.GetEmail(),
                HttpContext.RequestAborted);
            if (userId is null)
            {
                return Forbid();
            }

            try
            {
                await _workspaces.CreateAsync(
                    new CreateWorkspaceRequest(
                        userId.Value,
                        TenantId,
                        Name,
                        Description,
                        IsActive,
                        IsChatLlmEnabled,
                        ChatModelId,
                        IsRagLlmEnabled,
                        RagEmbeddingModelId,
                        IsOcrLlmEnabled,
                        OcrModelId),
                    HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is UsageLimitExceededException || ex is InvalidOperationException)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }
        else
        {
            try
            {
                await _workspaces.UpdateAsync(
                    new WorkspaceDto(
                        Id.Value,
                        TenantId,
                        Name,
                        Description,
                        IsActive,
                        IsChatLlmEnabled,
                        ChatModelId,
                        IsRagLlmEnabled,
                        RagEmbeddingModelId,
                        IsOcrLlmEnabled,
                        OcrModelId),
                    HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is UsageLimitExceededException || ex is InvalidOperationException)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }

        return RedirectToPage("/Workspaces/Index");
    }

    private async Task LoadTenantOptionsAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        var tenantIds = scope.EffectiveTenantIds.Where(scope.CanManageWorkspaces).ToList();
        var tenants = await _tenants.GetAsync(new TenantFilter
        {
            Ids = tenantIds,
            IncludeVisibleToAllUsers = true
        }, HttpContext.RequestAborted);

        TenantOptions = tenants.Select(t => new SelectListItem(t.Name, t.Id.ToString()));
    }

    private async Task LoadProviderOptionsAsync(Guid tenantId)
    {
        var effectiveTenantIds = await GetEffectiveTenantIdsAsync();
        if (!effectiveTenantIds.Contains(tenantId))
            throw new InvalidOperationException("Selected tenant is not in the current scope.");

        var users = await _users.GetByTenantIdAsync(tenantId, HttpContext.RequestAborted);
        var userIds = users.Select(u => u.Id).ToList();

        var chatOptions = await _providers.GetConfiguredByUsersAsync(userIds, LlmProviderType.Chat, HttpContext.RequestAborted);
        ChatModelOptions = BuildProviderOptions(chatOptions, ChatModelId);

        var ragOptions = await _providers.GetConfiguredByUsersAsync(userIds, LlmProviderType.RagEmbedding, HttpContext.RequestAborted);
        RagProviderOptions = BuildProviderOptions(ragOptions, RagEmbeddingModelId);

        var ocrOptions = await _providers.GetConfiguredByUsersAsync(userIds, LlmProviderType.Ocr, HttpContext.RequestAborted);
        OcrModelOptions = BuildProviderOptions(ocrOptions, OcrModelId);
    }

    private void ApplyCreateDefaultsForProviders()
    {
        if (Id is not null)
            return;

        IsChatLlmEnabled = HasChatModelOptions;
        if (!HasChatModelOptions)
            ChatModelId = null;
        else if (ChatModelOptions.Count() == 1)
            ChatModelId = Guid.Parse(ChatModelOptions.First().Value!);
        else
            ChatModelId = null;

        IsRagLlmEnabled = HasRagProviderOptions;
        if (!HasRagProviderOptions)
            RagEmbeddingModelId = null;
        else if (RagProviderOptions.Count() == 1)
            RagEmbeddingModelId = Guid.Parse(RagProviderOptions.First().Value!);
        else
            RagEmbeddingModelId = null;

        IsOcrLlmEnabled = HasOcrModelOptions;
        if (!HasOcrModelOptions)
            OcrModelId = null;
        else if (OcrModelOptions.Count() == 1)
            OcrModelId = Guid.Parse(OcrModelOptions.First().Value!);
        else
            OcrModelId = null;
    }

    private static IEnumerable<SelectListItem> BuildProviderOptions(
        IReadOnlyList<LlmProviderOptionDto> options,
        Guid? selectedProviderId)
    {
        var groups = options
            .Select(o => o.GroupName)
            .Distinct()
            .ToDictionary(name => name, name => new SelectListGroup { Name = name });

        return options.Select(o =>
        {
            var model = string.IsNullOrWhiteSpace(o.Model) ? "default model" : o.Model;
            var label = o.Scope == LlmConnectionScope.Hosted
                ? $"{o.GetName()} ({model})"
                : $"{o.UserEmail} - {o.GetName()} ({model})";
            if (o.IsDefault)
            {
                label += " [default]";
            }

            return new SelectListItem(label, o.Id.ToString(), selectedProviderId == o.Id)
            {
                Group = groups[o.GroupName]
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<Guid>> GetEffectiveTenantIdsAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        return scope.EffectiveTenantIds;
    }
}
