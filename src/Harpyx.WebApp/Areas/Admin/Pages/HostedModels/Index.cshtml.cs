using System.ComponentModel.DataAnnotations;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Harpyx.WebApp.Areas.Admin.Pages.HostedModels;

public class IndexModel : PageModel
{
    private readonly ILlmCatalogRepository _catalog;
    private readonly IEncryptionService _encryption;
    private readonly ILlmConnectionSmokeTestService _smokeTest;
    private readonly IUnitOfWork _unitOfWork;

    public IndexModel(
        ILlmCatalogRepository catalog,
        IEncryptionService encryption,
        ILlmConnectionSmokeTestService smokeTest,
        IUnitOfWork unitOfWork)
    {
        _catalog = catalog;
        _encryption = encryption;
        _smokeTest = smokeTest;
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public ConnectionInput Connection { get; set; } = new();

    [BindProperty]
    public ModelInput LocalModel { get; set; } = new();

    [BindProperty]
    public Guid DeleteConnectionId { get; set; }

    [BindProperty]
    public Guid DeleteModelId { get; set; }

    public IReadOnlyList<LlmConnection> Connections { get; private set; } = Array.Empty<LlmConnection>();
    public IEnumerable<SelectListItem> ProviderOptions { get; } = Enum.GetValues<LlmProvider>()
        .Where(p => p != LlmProvider.None)
        .Select(p => new SelectListItem(ProviderLabel(p), p.ToString()));

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSaveConnectionAsync()
    {
        if (Connection.Provider == LlmProvider.None)
        {
            ErrorMessage = "Select a provider.";
            await LoadAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Connection.Name))
        {
            ErrorMessage = "Connection name is required.";
            await LoadAsync();
            return Page();
        }

        if (RequiresBaseUrl(Connection.Provider) && string.IsNullOrWhiteSpace(Connection.BaseUrl))
        {
            ErrorMessage = $"{Connection.Provider} hosted connections require an endpoint/base URL.";
            await LoadAsync();
            return Page();
        }

        var connection = Connection.Id is Guid id && id != Guid.Empty
            ? await _catalog.GetConnectionByIdAsync(id, HttpContext.RequestAborted)
            : null;

        if (connection is null)
        {
            connection = new LlmConnection
            {
                Scope = LlmConnectionScope.Hosted,
                Provider = Connection.Provider
            };
            await _catalog.AddConnectionAsync(connection, HttpContext.RequestAborted);
        }
        else if (connection.Scope != LlmConnectionScope.Hosted)
        {
            ErrorMessage = "Only hosted connections can be edited here.";
            await LoadAsync();
            return Page();
        }

        if (RequiresApiKey(Connection.Provider) &&
            string.IsNullOrWhiteSpace(Connection.ApiKey) &&
            string.IsNullOrWhiteSpace(connection.EncryptedApiKey))
        {
            ErrorMessage = $"{Connection.Provider} hosted connections require an API key.";
            await LoadAsync();
            return Page();
        }

        var effectiveApiKey = string.IsNullOrWhiteSpace(Connection.ApiKey)
            ? string.IsNullOrWhiteSpace(connection.EncryptedApiKey)
                ? string.Empty
                : _encryption.Decrypt(connection.EncryptedApiKey)
            : Connection.ApiKey.Trim();

        connection.Provider = Connection.Provider;
        connection.Name = Connection.Name.Trim();
        connection.Description = Normalize(Connection.Description);
        connection.Notes = Normalize(Connection.Notes);
        connection.BaseUrl = RequiresBaseUrl(Connection.Provider)
            ? NormalizeBaseUrl(Connection.BaseUrl!)
            : null;
        connection.IsEnabled = Connection.IsEnabled;
        connection.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(Connection.ApiKey))
        {
            connection.EncryptedApiKey = _encryption.Encrypt(effectiveApiKey);
            connection.ApiKeyLast4 = effectiveApiKey.Length >= 4 ? effectiveApiKey[^4..] : effectiveApiKey;
        }

        if (connection.IsEnabled && connection.Models.Any(m => m.IsEnabled && m.IsPublished))
        {
            try
            {
                await _smokeTest.ValidateConfiguredModelsAsync(connection, effectiveApiKey, HttpContext.RequestAborted);
            }
            catch (ArgumentException ex)
            {
                ErrorMessage = ex.Message;
                await LoadAsync();
                return Page();
            }
        }

        _catalog.UpdateConnection(connection);
        await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);

        SuccessMessage = "Hosted connection saved.";
        return RedirectToPage("/HostedModels/Index", new { area = "Admin" });
    }

    public async Task<IActionResult> OnPostSaveModelAsync()
    {
        var connection = await _catalog.GetConnectionByIdAsync(LocalModel.ConnectionId, HttpContext.RequestAborted);
        if (connection is null || connection.Scope != LlmConnectionScope.Hosted)
        {
            ErrorMessage = "Select a valid hosted connection.";
            await LoadAsync();
            return Page();
        }

        if (LocalModel.Capability == LlmProviderType.RagEmbedding && !SupportsRagEmbedding(connection.Provider))
        {
            ErrorMessage = $"{connection.Provider} embeddings are not supported yet.";
            await LoadAsync();
            return Page();
        }

        if (LocalModel.Capability == LlmProviderType.Ocr && !SupportsOcr(connection.Provider))
        {
            ErrorMessage = $"{connection.Provider} OCR is not supported yet.";
            await LoadAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(LocalModel.ModelId))
        {
            ErrorMessage = "Model ID is required.";
            await LoadAsync();
            return Page();
        }

        var effectiveApiKey = string.IsNullOrWhiteSpace(connection.EncryptedApiKey)
            ? string.Empty
            : _encryption.Decrypt(connection.EncryptedApiKey);

        var candidate = new LlmModel
        {
            ConnectionId = connection.Id,
            Connection = connection,
            Capability = LocalModel.Capability,
            ModelId = LocalModel.ModelId.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(LocalModel.DisplayName)
                ? LocalModel.ModelId.Trim()
                : LocalModel.DisplayName.Trim(),
            EmbeddingDimensions = LocalModel.Capability == LlmProviderType.RagEmbedding
                ? LocalModel.EmbeddingDimensions
                : null,
            IsPublished = LocalModel.IsPublished,
            IsEnabled = LocalModel.IsEnabled
        };

        if (candidate.IsEnabled && candidate.IsPublished)
        {
            try
            {
                await _smokeTest.ValidateModelAsync(candidate, effectiveApiKey, HttpContext.RequestAborted);
            }
            catch (ArgumentException ex)
            {
                ErrorMessage = ex.Message;
                await LoadAsync();
                return Page();
            }
        }

        var model = LocalModel.Id is Guid id && id != Guid.Empty
            ? await _catalog.GetModelByIdAsync(id, HttpContext.RequestAborted)
            : null;

        if (model is null)
        {
            model = new LlmModel
            {
                ConnectionId = connection.Id
            };
            await _catalog.AddModelAsync(model, HttpContext.RequestAborted);
        }

        model.ConnectionId = connection.Id;
        model.Capability = candidate.Capability;
        model.ModelId = candidate.ModelId;
        model.DisplayName = candidate.DisplayName;
        model.EmbeddingDimensions = candidate.EmbeddingDimensions;
        model.IsPublished = candidate.IsPublished;
        model.IsEnabled = candidate.IsEnabled;
        model.UpdatedAt = DateTimeOffset.UtcNow;

        _catalog.UpdateModel(model);
        await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);

        SuccessMessage = "Hosted model saved.";
        return RedirectToPage("/HostedModels/Index", new { area = "Admin" });
    }

    public async Task<IActionResult> OnPostDeleteModelAsync()
    {
        var model = await _catalog.GetModelByIdAsync(DeleteModelId, HttpContext.RequestAborted);
        if (model is not null && model.Connection.Scope == LlmConnectionScope.Hosted)
        {
            _catalog.RemoveModel(model);
            await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
            SuccessMessage = "Hosted model removed.";
        }

        return RedirectToPage("/HostedModels/Index", new { area = "Admin" });
    }

    public async Task<IActionResult> OnPostDeleteConnectionAsync()
    {
        var connection = await _catalog.GetConnectionByIdAsync(DeleteConnectionId, HttpContext.RequestAborted);
        if (connection is not null && connection.Scope == LlmConnectionScope.Hosted)
        {
            _catalog.RemoveConnection(connection);
            await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
            SuccessMessage = "Hosted connection removed.";
        }

        return RedirectToPage("/HostedModels/Index", new { area = "Admin" });
    }

    private async Task LoadAsync()
    {
        Connections = await _catalog.GetHostedConnectionsAsync(HttpContext.RequestAborted);
        Connection = new ConnectionInput();
        LocalModel = new ModelInput
        {
            Capability = LlmProviderType.Chat,
            IsEnabled = true,
            IsPublished = true
        };
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeBaseUrl(string value)
        => value.Trim().TrimEnd('/');

    private static bool SupportsRagEmbedding(LlmProvider provider)
        => provider is LlmProvider.OpenAI or LlmProvider.Google or LlmProvider.OpenAICompatible or LlmProvider.AzureOpenAI;

    private static bool SupportsOcr(LlmProvider provider)
        => provider is LlmProvider.OpenAI or LlmProvider.Claude or LlmProvider.Google;

    private static bool RequiresBaseUrl(LlmProvider provider)
        => provider is LlmProvider.OpenAICompatible or LlmProvider.AzureOpenAI or LlmProvider.AmazonBedrock;

    private static bool RequiresApiKey(LlmProvider provider)
        => provider is not LlmProvider.OpenAICompatible;

    public static string ProviderLabel(LlmProvider provider)
        => provider switch
        {
            LlmProvider.AzureOpenAI => "Azure OpenAI",
            LlmProvider.AmazonBedrock => "Amazon Bedrock",
            LlmProvider.OpenAICompatible => "OpenAI-compatible",
            _ => provider.ToString()
        };

    public class ConnectionInput
    {
        public Guid? Id { get; set; }
        public LlmProvider Provider { get; set; } = LlmProvider.OpenAICompatible;

        [Required]
        public string? Name { get; set; }

        public string? Description { get; set; }
        public string? Notes { get; set; }

        [Required]
        public string? BaseUrl { get; set; }

        public string? ApiKey { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public class ModelInput
    {
        public Guid? Id { get; set; }
        public Guid ConnectionId { get; set; }
        public LlmProviderType Capability { get; set; } = LlmProviderType.Chat;
        public string? DisplayName { get; set; }

        [Required]
        public string? ModelId { get; set; }

        public int? EmbeddingDimensions { get; set; }
        public bool IsPublished { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
    }
}
