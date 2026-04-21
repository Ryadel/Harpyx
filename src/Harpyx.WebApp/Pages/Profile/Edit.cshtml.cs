using Harpyx.Application.DTOs;
using Harpyx.Application.Exceptions;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Profile;

public class EditModel : PageModel
{
    private readonly IUserLlmProviderService _providerService;
    private readonly IUserService _userService;

    public EditModel(IUserLlmProviderService providerService, IUserService userService)
    {
        _providerService = providerService;
        _userService = userService;
    }

    [BindProperty(SupportsGet = true)]
    public LlmProvider Provider { get; set; } = LlmProvider.None;

    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    [BindProperty]
    public string? ApiKey { get; set; }

    [BindProperty]
    public string? BaseUrl { get; set; }

    [BindProperty]
    public bool EnableChat { get; set; } = true;

    [BindProperty]
    public bool EnableRagEmbedding { get; set; }

    [BindProperty]
    public bool EnableOcr { get; set; }

    [BindProperty]
    public string? ChatModel { get; set; }

    [BindProperty]
    public string? RagEmbeddingModel { get; set; }

    [BindProperty]
    public string? OcrModel { get; set; }

    [BindProperty]
    public bool SetAsDefaultChat { get; set; } = true;

    [BindProperty]
    public bool SetAsDefaultRagEmbedding { get; set; } = true;

    [BindProperty]
    public bool SetAsDefaultOcr { get; set; } = true;

    public string Title => IsEditMode ? "Edit LLM Provider" : "Add LLM Provider";
    public bool IsEditMode => Provider != LlmProvider.None;
    public string ProviderDisplayName { get; private set; } = string.Empty;

    public IEnumerable<SelectListItem> ProviderOptions { get; } = Enum.GetValues<LlmProvider>()
        .Where(p => p != LlmProvider.None && p != LlmProvider.OpenAICompatible)
        .Select(p => new SelectListItem(ProviderLabel(p), p.ToString()));

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return RedirectToPage("/Index");

        if (!IsEditMode)
        {
            ProviderDisplayName = Provider.ToString();
            return Page();
        }

        var result = await _providerService.GetAllAsync(userId.Value, HttpContext.RequestAborted);
        var llmProvider = result.Providers.FirstOrDefault(p => p.Provider == Provider);
        if (llmProvider is null)
        {
            TempData["ErrorMessage"] = "The selected provider was not found.";
            return RedirectToPage("/Profile/Index");
        }

        EnableChat = llmProvider.SupportsChat;
        EnableRagEmbedding = llmProvider.SupportsRagEmbedding;
        EnableOcr = llmProvider.SupportsOcr;
        Name = llmProvider.Name;
        Description = llmProvider.Description;
        Notes = llmProvider.Notes;
        BaseUrl = llmProvider.BaseUrl;
        ChatModel = llmProvider.ChatModel;
        RagEmbeddingModel = llmProvider.RagEmbeddingModel;
        OcrModel = llmProvider.OcrModel;
        SetAsDefaultChat = llmProvider.IsDefaultChat;
        SetAsDefaultRagEmbedding = llmProvider.IsDefaultRagEmbedding;
        SetAsDefaultOcr = llmProvider.IsDefaultOcr;
        ProviderDisplayName = llmProvider.GetName();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return RedirectToPage("/Index");

        if (Provider == LlmProvider.None)
        {
            ErrorMessage = "Please select a provider.";
            return Page();
        }

        ProviderDisplayName = string.IsNullOrWhiteSpace(Name)
            ? Provider.ToString()
            : $"{Name.Trim()} - {Provider}";

        if (!EnableChat && !EnableRagEmbedding && !EnableOcr)
        {
            ErrorMessage = "Enable at least one capability: Chat, RAG Embedding and/or OCR.";
            return Page();
        }

        if (RequiresBaseUrl(Provider) && string.IsNullOrWhiteSpace(BaseUrl))
        {
            ErrorMessage = $"{Provider} requires an endpoint/base URL.";
            return Page();
        }

        if (EnableOcr && !SupportsOcr(Provider))
        {
            ErrorMessage = $"{Provider} is not supported for OCR yet.";
            return Page();
        }

        var request = new LlmProviderSaveRequest(
            Provider,
            ApiKey,
            EnableChat,
            EnableRagEmbedding,
            EnableOcr,
            ChatModel,
            RagEmbeddingModel,
            OcrModel,
            SetAsDefaultChat,
            SetAsDefaultRagEmbedding,
            SetAsDefaultOcr,
            Name,
            Description,
            Notes,
            BaseUrl);

        try
        {
            await _providerService.SaveAsync(userId.Value, request, HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is UsageLimitExceededException)
        {
            ErrorMessage = ex.Message;
            return Page();
        }

        TempData["SuccessMessage"] = IsEditMode
            ? "Provider configuration updated successfully."
            : "Provider configuration created successfully.";

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

    private static bool RequiresBaseUrl(LlmProvider provider)
        => provider is LlmProvider.AzureOpenAI or LlmProvider.AmazonBedrock;

    private static bool SupportsOcr(LlmProvider provider)
        => provider is LlmProvider.OpenAI or LlmProvider.Claude or LlmProvider.Google;

    public static string ProviderLabel(LlmProvider provider)
        => provider switch
        {
            LlmProvider.AzureOpenAI => "Azure OpenAI",
            LlmProvider.AmazonBedrock => "Amazon Bedrock",
            _ => provider.ToString()
        };
}
