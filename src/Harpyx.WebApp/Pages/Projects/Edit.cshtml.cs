using Harpyx.Application.DTOs;
using Harpyx.Application.Defaults;
using Harpyx.Application.Exceptions;
using Harpyx.Application.Filters;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Projects;

public class EditModel : PageModel
{
    private readonly IProjectService _projects;
    private readonly IUserService _users;
    private readonly IWorkspaceService _workspaces;
    private readonly ITenantService _tenants;
    private readonly IUserLlmProviderService _providers;
    private readonly ITenantScopeService _tenantScope;

    public EditModel(
        IProjectService projects,
        IUserService users,
        IWorkspaceService workspaces,
        ITenantService tenants,
        IUserLlmProviderService providers,
        ITenantScopeService tenantScope)
    {
        _projects = projects;
        _users = users;
        _workspaces = workspaces;
        _tenants = tenants;
        _providers = providers;
        _tenantScope = tenantScope;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid WorkspaceId { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public LlmFeatureOverride ChatLlmOverride { get; set; } = LlmFeatureOverride.WorkspaceDefault;

    [BindProperty]
    public Guid? ChatModelId { get; set; }

    [BindProperty]
    public LlmFeatureOverride RagLlmOverride { get; set; } = LlmFeatureOverride.WorkspaceDefault;

    [BindProperty]
    public Guid? RagEmbeddingModelId { get; set; }

    [BindProperty]
    public LlmFeatureOverride OcrLlmOverride { get; set; } = LlmFeatureOverride.WorkspaceDefault;

    [BindProperty]
    public Guid? OcrModelId { get; set; }

    [BindProperty]
    public ProjectLifetimePreset? LifetimePreset { get; set; } = ProjectLifetimeDefaults.DefaultPreset;

    [BindProperty]
    public bool AutoExtendLifetimeOnActivity { get; set; } = ProjectLifetimeDefaults.DefaultAutoExtendOnActivity;

    [BindProperty]
    public bool ConfirmRagReindex { get; set; }

    public IEnumerable<SelectListItem> WorkspaceOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> ChatOverrideOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> ChatModelOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> RagOverrideOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> RagProviderOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> OcrOverrideOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> OcrModelOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> LifetimePresetOptions { get; private set; } = Array.Empty<SelectListItem>();
    public bool HasChatModelOptions => ChatModelOptions.Any();
    public bool HasRagProviderOptions => RagProviderOptions.Any();
    public bool HasOcrModelOptions => OcrModelOptions.Any();
    public bool HasWorkspaceOptions => WorkspaceOptions.Any();
    public bool IsCreateMode => Id is null;
    public bool MustCreateWorkspaceFirst => IsCreateMode && !HasWorkspaceOptions;
    public bool WorkspaceChatDefaultEnabled { get; private set; }
    public bool WorkspaceRagDefaultEnabled { get; private set; }
    public bool WorkspaceOcrDefaultEnabled { get; private set; }
    public string? ErrorMessage { get; private set; }

    public string Title => Id is null ? "Create Project" : "Edit Project";

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadWorkspaceOptionsAsync();

        if (Id is null)
        {
            if (WorkspaceOptions.Any() && WorkspaceId == Guid.Empty)
            {
                WorkspaceId = Guid.Parse(WorkspaceOptions.First().Value!);
            }

            if (WorkspaceId != Guid.Empty)
            {
                await LoadProviderOptionsForWorkspaceAsync(WorkspaceId);
                ChatOverrideOptions = BuildOverrideOptions(ChatLlmOverride, HasChatModelOptions, WorkspaceChatDefaultEnabled, "Chat LLM");
                RagOverrideOptions = BuildOverrideOptions(RagLlmOverride, HasRagProviderOptions, WorkspaceRagDefaultEnabled, "RAG");
                OcrOverrideOptions = BuildOverrideOptions(OcrLlmOverride, HasOcrModelOptions, WorkspaceOcrDefaultEnabled, "OCR LLM");
            }
            else
            {
                ChatModelOptions = Array.Empty<SelectListItem>();
                RagProviderOptions = Array.Empty<SelectListItem>();
                OcrModelOptions = Array.Empty<SelectListItem>();
                ChatOverrideOptions = BuildOverrideOptions(ChatLlmOverride, false, false, "Chat LLM");
                RagOverrideOptions = BuildOverrideOptions(RagLlmOverride, false, false, "RAG");
                OcrOverrideOptions = BuildOverrideOptions(OcrLlmOverride, false, false, "OCR LLM");
            }

            LifetimePresetOptions = BuildLifetimePresetOptions(LifetimePreset);
            return Page();
        }

        var project = await _projects.GetByIdAsync(Id.Value, HttpContext.RequestAborted);
        if (project is null)
            return RedirectToPage("/Projects/Index");

        var workspace = await _workspaces.GetByIdAsync(project.WorkspaceId, HttpContext.RequestAborted);
        if (workspace is null)
            return RedirectToPage("/Projects/Index");

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.IsTenantInScope(workspace.TenantId))
            return Forbid();
        if (!scope.CanManageProjects(workspace.TenantId))
            return Forbid();

        WorkspaceId = project.WorkspaceId;
        Name = project.Name;
        Description = project.Description;
        ChatLlmOverride = project.ChatLlmOverride;
        ChatModelId = project.ChatModelId;
        RagLlmOverride = project.RagLlmOverride;
        RagEmbeddingModelId = project.RagEmbeddingModelId;
        OcrLlmOverride = project.OcrLlmOverride;
        OcrModelId = project.OcrModelId;
        LifetimePreset = project.LifetimePreset;
        AutoExtendLifetimeOnActivity = project.AutoExtendLifetimeOnActivity;

        await LoadProviderOptionsForWorkspaceAsync(project.WorkspaceId);
        if (!HasChatModelOptions && ChatLlmOverride == LlmFeatureOverride.Enabled)
        {
            ChatLlmOverride = LlmFeatureOverride.Disabled;
            ChatModelId = null;
        }

        if (!HasRagProviderOptions && RagLlmOverride == LlmFeatureOverride.Enabled)
        {
            RagLlmOverride = LlmFeatureOverride.Disabled;
            RagEmbeddingModelId = null;
        }

        if (!HasOcrModelOptions && OcrLlmOverride == LlmFeatureOverride.Enabled)
        {
            OcrLlmOverride = LlmFeatureOverride.Disabled;
            OcrModelId = null;
        }

        ChatOverrideOptions = BuildOverrideOptions(ChatLlmOverride, HasChatModelOptions, WorkspaceChatDefaultEnabled, "Chat LLM");
        RagOverrideOptions = BuildOverrideOptions(RagLlmOverride, HasRagProviderOptions, WorkspaceRagDefaultEnabled, "RAG");
        OcrOverrideOptions = BuildOverrideOptions(OcrLlmOverride, HasOcrModelOptions, WorkspaceOcrDefaultEnabled, "OCR LLM");
        LifetimePresetOptions = BuildLifetimePresetOptions(LifetimePreset);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadWorkspaceOptionsAsync();
        if (MustCreateWorkspaceFirst)
        {
            ErrorMessage = "You must create a workspace first.";
            return Page();
        }

        if (WorkspaceId == Guid.Empty && WorkspaceOptions.Any())
        {
            WorkspaceId = Guid.Parse(WorkspaceOptions.First().Value!);
        }

        if (WorkspaceId != Guid.Empty)
        {
            await LoadProviderOptionsForWorkspaceAsync(WorkspaceId);
        }
        else
        {
            ChatModelOptions = Array.Empty<SelectListItem>();
            RagProviderOptions = Array.Empty<SelectListItem>();
            OcrModelOptions = Array.Empty<SelectListItem>();
            WorkspaceChatDefaultEnabled = false;
            WorkspaceRagDefaultEnabled = false;
            WorkspaceOcrDefaultEnabled = false;
        }

        if (!HasChatModelOptions && ChatLlmOverride == LlmFeatureOverride.Enabled)
        {
            ChatLlmOverride = LlmFeatureOverride.Disabled;
            ChatModelId = null;
        }

        if (!HasRagProviderOptions && RagLlmOverride == LlmFeatureOverride.Enabled)
        {
            RagLlmOverride = LlmFeatureOverride.Disabled;
            RagEmbeddingModelId = null;
        }

        if (!HasOcrModelOptions && OcrLlmOverride == LlmFeatureOverride.Enabled)
        {
            OcrLlmOverride = LlmFeatureOverride.Disabled;
            OcrModelId = null;
        }

        if (ChatLlmOverride != LlmFeatureOverride.Enabled)
            ChatModelId = null;

        if (RagLlmOverride != LlmFeatureOverride.Enabled)
            RagEmbeddingModelId = null;

        if (OcrLlmOverride != LlmFeatureOverride.Enabled)
            OcrModelId = null;
        if (LifetimePreset is null)
            AutoExtendLifetimeOnActivity = false;

        ChatOverrideOptions = BuildOverrideOptions(ChatLlmOverride, HasChatModelOptions, WorkspaceChatDefaultEnabled, "Chat LLM");
        RagOverrideOptions = BuildOverrideOptions(RagLlmOverride, HasRagProviderOptions, WorkspaceRagDefaultEnabled, "RAG");
        OcrOverrideOptions = BuildOverrideOptions(OcrLlmOverride, HasOcrModelOptions, WorkspaceOcrDefaultEnabled, "OCR LLM");
        LifetimePresetOptions = BuildLifetimePresetOptions(LifetimePreset);

        var targetWorkspace = await _workspaces.GetByIdAsync(WorkspaceId, HttpContext.RequestAborted);
        if (targetWorkspace is null)
        {
            ErrorMessage = "Selected workspace not found.";
            return Page();
        }

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.CanManageProjects(targetWorkspace.TenantId))
            return Forbid();

        if (Id is Guid projectId)
        {
            var existingProject = await _projects.GetByIdAsync(projectId, HttpContext.RequestAborted);
            if (existingProject is not null)
            {
                var sourceWorkspace = await _workspaces.GetByIdAsync(existingProject.WorkspaceId, HttpContext.RequestAborted);
                if (sourceWorkspace is not null && !scope.CanManageProjects(sourceWorkspace.TenantId))
                    return Forbid();
            }
        }

        try
        {
            if (Id is null)
            {
                var userId = await _users.ResolveUserIdAsync(
                    User.GetObjectId(),
                    User.GetSubjectId(),
                    User.GetEmail(),
                    HttpContext.RequestAborted);
                if (userId is null)
                    return Forbid();

                await _projects.CreateAsync(
                    new CreateProjectRequest(
                        WorkspaceId,
                        Name,
                        Description,
                        ChatLlmOverride,
                        ChatModelId,
                        RagLlmOverride,
                        RagEmbeddingModelId,
                        OcrLlmOverride,
                        OcrModelId,
                        LifetimePreset,
                        AutoExtendLifetimeOnActivity,
                        ConfirmRagReindex: false,
                        CreatedByUserId: userId.Value),
                    HttpContext.RequestAborted);
            }
            else
            {
                await _projects.UpdateAsync(
                    new ProjectDto(
                        Id.Value,
                        WorkspaceId,
                        Name,
                        Description,
                        ChatLlmOverride,
                        ChatModelId,
                        RagLlmOverride,
                        RagEmbeddingModelId,
                        OcrLlmOverride,
                        OcrModelId,
                        LifetimePreset,
                        null,
                        AutoExtendLifetimeOnActivity,
                        ConfirmRagReindex),
                    HttpContext.RequestAborted);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is UsageLimitExceededException)
        {
            ErrorMessage = ex.Message;
            return Page();
        }

        return RedirectToPage("/Projects/Index");
    }

    private async Task LoadWorkspaceOptionsAsync()
    {
        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        var tenantIds = scope.EffectiveTenantIds.Where(scope.CanManageProjects).ToList();
        var tenants = await _tenants.GetAsync(new TenantFilter
        {
            Ids = tenantIds,
            IncludeVisibleToAllUsers = true
        }, HttpContext.RequestAborted);

        var tenantNamesById = tenants.ToDictionary(t => t.Id, t => t.Name);
        var workspaces = await _workspaces.GetAllAsync(tenantIds, HttpContext.RequestAborted);
        WorkspaceOptions = workspaces.Select(w =>
        {
            var tenantName = tenantNamesById.TryGetValue(w.TenantId, out var name) ? name : "Unknown tenant";
            // return new SelectListItem($"[{tenantName}] {w.Name}", w.Id.ToString());
            return new SelectListItem($"{w.Name}", w.Id.ToString());
        }).ToList();
    }

    private async Task LoadProviderOptionsForWorkspaceAsync(Guid workspaceId)
    {
        var workspace = await _workspaces.GetByIdAsync(workspaceId, HttpContext.RequestAborted) ??
            throw new InvalidOperationException("Selected workspace not found.");
        WorkspaceChatDefaultEnabled = workspace.IsChatLlmEnabled && workspace.ChatModelId is not null;
        WorkspaceRagDefaultEnabled = workspace.IsRagLlmEnabled && workspace.RagEmbeddingModelId is not null;
        WorkspaceOcrDefaultEnabled = workspace.IsOcrLlmEnabled && workspace.OcrModelId is not null;

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.IsTenantInScope(workspace.TenantId))
            throw new InvalidOperationException("Selected workspace is not accessible.");

        var users = await _users.GetByTenantIdAsync(workspace.TenantId, HttpContext.RequestAborted);
        var chatOptions = await _providers.GetConfiguredByUsersAsync(
            users.Select(u => u.Id).ToList(),
            LlmProviderType.Chat,
            HttpContext.RequestAborted);
        ChatModelOptions = BuildProviderOptions(chatOptions, ChatModelId);

        var ragOptions = await _providers.GetConfiguredByUsersAsync(
            users.Select(u => u.Id).ToList(),
            LlmProviderType.RagEmbedding,
            HttpContext.RequestAborted);
        RagProviderOptions = BuildProviderOptions(ragOptions, RagEmbeddingModelId);

        var ocrOptions = await _providers.GetConfiguredByUsersAsync(
            users.Select(u => u.Id).ToList(),
            LlmProviderType.Ocr,
            HttpContext.RequestAborted);
        OcrModelOptions = BuildProviderOptions(ocrOptions, OcrModelId);
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
        });
    }

    private static IEnumerable<SelectListItem> BuildOverrideOptions(
        LlmFeatureOverride currentValue,
        bool hasSpecializedProviders,
        bool workspaceDefaultEnabled,
        string featureLabel)
    {
        var workspaceLabel = workspaceDefaultEnabled
            ? $"Workspace Default ({featureLabel} Enabled)"
            : $"Workspace Default ({featureLabel} Disabled)";

        return new List<SelectListItem>
        {
            new(workspaceLabel, LlmFeatureOverride.WorkspaceDefault.ToString(), currentValue == LlmFeatureOverride.WorkspaceDefault),
            new("Enabled", LlmFeatureOverride.Enabled.ToString(), currentValue == LlmFeatureOverride.Enabled) { Disabled = !hasSpecializedProviders },
            new("Disabled", LlmFeatureOverride.Disabled.ToString(), currentValue == LlmFeatureOverride.Disabled)
        };
    }

    private static IEnumerable<SelectListItem> BuildLifetimePresetOptions(ProjectLifetimePreset? selected)
    {
        return ProjectLifetimeDefaults.OrderedPresets.Select(preset =>
            new SelectListItem(
                ProjectLifetimeDefaults.GetLabel(preset),
                preset.ToString(),
                selected == preset));
    }
}
