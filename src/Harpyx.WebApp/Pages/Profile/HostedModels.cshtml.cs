using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Profile;

public class HostedModelsModel : PageModel
{
    private readonly IUserLlmProviderService _providerService;
    private readonly IUserService _userService;

    public HostedModelsModel(IUserLlmProviderService providerService, IUserService userService)
    {
        _providerService = providerService;
        _userService = userService;
    }

    [BindProperty]
    public Guid? ChatModelId { get; set; }

    [BindProperty]
    public Guid? RagEmbeddingModelId { get; set; }

    [BindProperty]
    public Guid? OcrModelId { get; set; }

    public IReadOnlyList<HostedLlmModelDto> Models { get; private set; } = Array.Empty<HostedLlmModelDto>();
    public IEnumerable<SelectListItem> ChatOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> RagOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> OcrOptions { get; private set; } = Array.Empty<SelectListItem>();
    public bool HasAnyModels => Models.Count > 0;

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

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return RedirectToPage("/Index");

        try
        {
            await _providerService.SaveHostedModelSelectionAsync(
                userId.Value,
                new HostedLlmModelSelectionRequest(
                    ChatModelId,
                    RagEmbeddingModelId,
                    OcrModelId,
                    ChatModelId is not null,
                    RagEmbeddingModelId is not null,
                    OcrModelId is not null),
                HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadAsync(userId.Value);
            return Page();
        }

        SuccessMessage = "Hosted model preferences updated.";
        return RedirectToPage("/Profile/HostedModels");
    }

    private async Task LoadAsync(Guid userId)
    {
        Models = await _providerService.GetAvailableHostedModelsAsync(userId, HttpContext.RequestAborted);
        ChatModelId = Models.FirstOrDefault(m => m.Capability == LlmProviderType.Chat && m.IsDefault)?.Id;
        RagEmbeddingModelId = Models.FirstOrDefault(m => m.Capability == LlmProviderType.RagEmbedding && m.IsDefault)?.Id;
        OcrModelId = Models.FirstOrDefault(m => m.Capability == LlmProviderType.Ocr && m.IsDefault)?.Id;

        ChatOptions = BuildOptions(LlmProviderType.Chat, ChatModelId);
        RagOptions = BuildOptions(LlmProviderType.RagEmbedding, RagEmbeddingModelId);
        OcrOptions = BuildOptions(LlmProviderType.Ocr, OcrModelId);
    }

    private IEnumerable<SelectListItem> BuildOptions(LlmProviderType capability, Guid? selectedId)
    {
        return Models
            .Where(m => m.Capability == capability)
            .Select(m => new SelectListItem(
                $"{m.DisplayName} ({m.ModelId})",
                m.Id.ToString(),
                selectedId == m.Id));
    }

    private async Task<Guid?> ResolveUserIdAsync()
    {
        return await _userService.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            HttpContext.RequestAborted);
    }
}
