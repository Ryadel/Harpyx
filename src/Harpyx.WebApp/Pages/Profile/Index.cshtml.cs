using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Profile;

public class IndexModel : PageModel
{
    private readonly IUserLlmProviderService _providerService;
    private readonly IUserService _userService;

    public IndexModel(IUserLlmProviderService providerService, IUserService userService)
    {
        _providerService = providerService;
        _userService = userService;
    }

    [BindProperty]
    public LlmProvider DeleteProvider { get; set; }

    [BindProperty]
    public LlmProviderType SetDefaultUsage { get; set; } = LlmProviderType.Chat;

    [BindProperty]
    public Guid SetDefaultModelId { get; set; }

    public IReadOnlyList<LlmProviderDto> Providers { get; private set; } = Array.Empty<LlmProviderDto>();
    public bool HasChatModelConfigured { get; private set; }
    public bool HasRagProviderConfigured { get; private set; }
    public bool HasOcrModelConfigured { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null) return RedirectToPage("/Index");

        await LoadProvidersAsync(userId.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null) return RedirectToPage("/Index");

        var result = await _providerService.DeleteAsync(userId.Value, DeleteProvider, HttpContext.RequestAborted);
        SuccessMessage = result.AffectedProjectCount > 0
            ? $"Provider removed. {result.AffectedProjectCount} project index(es) were reset."
            : "Provider removed.";

        return RedirectToPage("/Profile/Index");
    }

    public async Task<IActionResult> OnPostSetDefaultAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null) return RedirectToPage("/Index");

        try
        {
            await _providerService.SetDefaultAsync(userId.Value, SetDefaultUsage, SetDefaultModelId, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage("/Profile/Index");
        }

        var usageLabel = SetDefaultUsage switch
        {
            LlmProviderType.Chat => "Chat",
            LlmProviderType.RagEmbedding => "RAG Embedding",
            LlmProviderType.Ocr => "OCR",
            _ => SetDefaultUsage.ToString()
        };
        var providers = await _providerService.GetAllAsync(userId.Value, HttpContext.RequestAborted);
        var selected = providers.Providers.FirstOrDefault(p =>
            p.ChatModelId == SetDefaultModelId ||
            p.RagEmbeddingModelId == SetDefaultModelId ||
            p.OcrModelId == SetDefaultModelId);
        var providerName = selected?.GetName() ?? "Selected model";
        SuccessMessage = $"{providerName} set as default {usageLabel} model.";

        return RedirectToPage("/Profile/Index");
    }

    private async Task<Guid?> ResolveUserIdAsync()
    {
        return await _userService.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            HttpContext.RequestAborted);
    }

    private async Task LoadProvidersAsync(Guid userId)
    {
        var result = await _providerService.GetAllAsync(userId, HttpContext.RequestAborted);
        Providers = result.Providers;
        HasChatModelConfigured = result.HasAnyChatConfigured;
        HasRagProviderConfigured = result.HasAnyRagConfigured;
        HasOcrModelConfigured = result.HasAnyOcrConfigured;
    }
}
