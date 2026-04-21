using System.Text.Json;
using System.Text;
using Harpyx.Application.Defaults;
using Harpyx.Application.DTOs;
using Harpyx.Application.Exceptions;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.Infrastructure.Services;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages.Projects;

public class DetailsModel : PageModel
{
    private const int PromptPreviewLength = 96;
    private static readonly JsonSerializerOptions StreamJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public record PromptLibraryItemPayload(
        Guid Id,
        ProjectPromptType PromptType,
        bool IsFavorite,
        string Preview,
        DateTimeOffset LastUsedAt);

    public record PromptLibrariesPayload(
        IReadOnlyList<PromptLibraryItemPayload> System,
        IReadOnlyList<PromptLibraryItemPayload> User);

    private readonly IProjectService _projects;
    private readonly IWorkspaceService _workspaces;
    private readonly IDocumentService _documents;
    private readonly IUserService _users;
    private readonly IUserLlmProviderService _providers;
    private readonly ILlmClientResolver _llmResolver;
    private readonly IRagRetrievalService _ragRetrieval;
    private readonly IProjectPromptService _projectPrompts;
    private readonly IProjectChatMessageService _chatMessages;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly ITenantScopeService _tenantScope;

    public DetailsModel(
        IProjectService projects,
        IWorkspaceService workspaces,
        IDocumentService documents,
        IUserService users,
        IUserLlmProviderService providers,
        ILlmClientResolver llmResolver,
        IRagRetrievalService ragRetrieval,
        IProjectPromptService projectPrompts,
        IProjectChatMessageService chatMessages,
        IPlatformSettingsService platformSettings,
        ITenantScopeService tenantScope,
        IOptions<UploadSecurityOptions> uploadSecurityOptions)
    {
        _projects = projects;
        _workspaces = workspaces;
        _documents = documents;
        _users = users;
        _providers = providers;
        _llmResolver = llmResolver;
        _ragRetrieval = ragRetrieval;
        _projectPrompts = projectPrompts;
        _chatMessages = chatMessages;
        _platformSettings = platformSettings;
        _tenantScope = tenantScope;
        SupportedExtensions = (uploadSecurityOptions.Value.AllowedExtensions ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => NormalizeExtension(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    [BindProperty]
    public Guid DeleteDocumentId { get; set; }

    [BindProperty]
    public Guid RetryDocumentId { get; set; }

    [BindProperty]
    public Guid RenameDocumentId { get; set; }

    [BindProperty]
    public string? RenameFileName { get; set; }

    [BindProperty]
    public string? DocumentUrl { get; set; }

    [BindProperty]
    public List<Guid> SelectedDocumentIds { get; set; } = new();

    [BindProperty]
    public string? SystemPrompt { get; set; }

    [BindProperty]
    public string? UserPrompt { get; set; }

    [BindProperty]
    public Guid? SelectedModelId { get; set; }

    [BindProperty]
    public string? ChatHistoryJson { get; set; }

    public ProjectDto? Project { get; private set; }
    private WorkspaceDto? Workspace { get; set; }
    public Guid? ProjectTenantId { get; private set; }
    public IReadOnlyList<DocumentDto> Documents { get; private set; } = Array.Empty<DocumentDto>();
    public List<ChatMessage> ChatHistory { get; private set; } = new();
    public IEnumerable<SelectListItem> ModelOptions { get; private set; } = Array.Empty<SelectListItem>();
    public string DefaultModelOptionLabel { get; private set; } = "Default model";
    public bool HasLlmConfigured { get; private set; }
    public IReadOnlyList<string> SupportedExtensions { get; }
    public int SystemPromptMaxLengthChars { get; private set; } = PromptDefaults.SystemPromptMaxLengthChars;
    public int UserPromptMaxLengthChars { get; private set; } = PromptDefaults.UserPromptMaxLengthChars;
    public string EffectiveDefaultSystemPrompt { get; private set; } = PromptDefaults.DefaultSystemPrompt;
    public ProjectPromptCollectionDto SystemPromptCollection { get; private set; } = ProjectPromptCollectionDto.Empty;
    public ProjectPromptCollectionDto UserPromptCollection { get; private set; } = ProjectPromptCollectionDto.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public IReadOnlyList<ProjectPromptDto> GetSystemPromptLibraryItems()
        => MergePromptCollection(SystemPromptCollection);

    public IReadOnlyList<ProjectPromptDto> GetUserPromptLibraryItems()
        => MergePromptCollection(UserPromptCollection);

    public string GetPromptPreview(string content)
        => CreatePromptPreview(content, PromptPreviewLength);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        var uploadScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid uploadTenantId || !uploadScope.CanManageProjects(uploadTenantId))
            return Forbid();

        await LoadDashboardAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        var deleteScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid deleteTenantId || !deleteScope.CanManageProjects(deleteTenantId))
            return Forbid();

        if (UploadFile is null || UploadFile.Length == 0)
        {
            ErrorMessage = "Please select a file to upload.";
            await LoadDashboardAsync();
            return Page();
        }

        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return Unauthorized();

        await using var stream = UploadFile.OpenReadStream();
        try
        {
            var result = await _documents.UploadAsync(
                new UploadDocumentRequest(userId.Value, Id, UploadFile.FileName, UploadFile.ContentType, stream, UploadFile.Length),
                HttpContext.RequestAborted);

            SuccessMessage = result.State switch
            {
                DocumentState.Uploaded => "Document uploaded.",
                DocumentState.Queued => "Document uploaded and queued for indexing.",
                DocumentState.Quarantined => "Document was quarantined by malware protection and is not available for RAG/chat.",
                DocumentState.Rejected => "Document was rejected by upload security checks and is not available for RAG/chat.",
                _ => $"Document uploaded with state {result.State}."
            };
        }
        catch (UsageLimitExceededException ex)
        {
            ErrorMessage = ex.Message;
            await LoadDashboardAsync();
            return Page();
        }

        UploadFile = null;
        await LoadDashboardAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddUrlAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid tenantId || !scope.CanManageProjects(tenantId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(DocumentUrl))
        {
            ErrorMessage = "Please enter a URL.";
            await LoadDashboardAsync();
            return Page();
        }

        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return Unauthorized();

        try
        {
            await _documents.AddUrlAsync(
                new AddUrlDocumentRequest(Id, userId.Value, DocumentUrl.Trim()),
                HttpContext.RequestAborted);
            SuccessMessage = "URL added. The document will be fetched and processed.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ErrorMessage = ex.Message;
        }

        DocumentUrl = null;
        await LoadDashboardAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        var renameScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid renameTenantId || !renameScope.CanManageProjects(renameTenantId))
            return Forbid();

        if (DeleteDocumentId == Guid.Empty)
        {
            ErrorMessage = "Invalid document selected.";
            await LoadDashboardAsync();
            return Page();
        }

        await _documents.DeleteAsync(DeleteDocumentId, HttpContext.RequestAborted);
        SuccessMessage = "Document deleted.";
        await LoadDashboardAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRetryAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        var retryScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid retryTenantId || !retryScope.CanManageProjects(retryTenantId))
            return Forbid();

        if (RetryDocumentId == Guid.Empty)
        {
            ErrorMessage = "Invalid document selected.";
            await LoadDashboardAsync();
            return Page();
        }

        try
        {
            var retried = await _documents.RetryAsync(RetryDocumentId, HttpContext.RequestAborted);
            if (!retried)
            {
                ErrorMessage = "Document not found.";
                await LoadDashboardAsync();
                return Page();
            }
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadDashboardAsync();
            return Page();
        }

        SuccessMessage = "Document queued again for retry.";
        await LoadDashboardAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRenameAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        var chatScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid chatTenantId || !chatScope.CanUseChat(chatTenantId))
            return Forbid();

        if (RenameDocumentId == Guid.Empty || string.IsNullOrWhiteSpace(RenameFileName))
        {
            ErrorMessage = "Please provide a valid file name.";
            await LoadDashboardAsync();
            return Page();
        }

        await _documents.RenameAsync(RenameDocumentId, RenameFileName, HttpContext.RequestAborted);
        SuccessMessage = "Document renamed.";
        await LoadDashboardAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostChatAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        await LoadDashboardAsync(preserveSelection: true, preservePromptInputs: true);

        var userPrompt = NormalizePrompt(UserPrompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            ErrorMessage = "Please enter a user prompt.";
            return Page();
        }

        var systemPrompt = ResolveSystemPromptForChat();

        if (!HasLlmConfigured)
        {
            ErrorMessage = "No LLM model configured. Please update your Profile.";
            return Page();
        }

        if (SelectedDocumentIds.Count == 0)
        {
            ErrorMessage = "Select at least one document to provide context.";
            return Page();
        }

        if (systemPrompt.Length > SystemPromptMaxLengthChars)
        {
            ErrorMessage = $"System prompt exceeds the maximum length ({SystemPromptMaxLengthChars} characters).";
            return Page();
        }

        if (userPrompt.Length > UserPromptMaxLengthChars)
        {
            ErrorMessage = $"User prompt exceeds the maximum length ({UserPromptMaxLengthChars} characters).";
            return Page();
        }

        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return Unauthorized();

        var resolve = await _llmResolver.ResolveAsync(
            userId.Value,
            SelectedModelId,
            ResolveEffectiveDefaultChatModelId(),
            HttpContext.RequestAborted);
        if (!resolve.IsConfigured || resolve.Client is null)
        {
            ErrorMessage = "Selected LLM model is not configured.";
            return Page();
        }

        var rag = await _ragRetrieval.BuildContextAsync(Id, SelectedDocumentIds, userPrompt, HttpContext.RequestAborted);
        var effectiveSystemPrompt = $"{systemPrompt}\n\nRAG context:\n{rag.Context}";

        ChatHistory = ReadChatHistory();
        ChatHistory.Add(new ChatMessage("user", userPrompt, DateTimeOffset.UtcNow));
        ChatHistoryJson = JsonSerializer.Serialize(ChatHistory);

        try
        {
            await _projectPrompts.SavePromptUsageAsync(Id, ProjectPromptType.System, systemPrompt, HttpContext.RequestAborted);
            await _projectPrompts.SavePromptUsageAsync(Id, ProjectPromptType.User, userPrompt, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }

        var result = await resolve.Client.ChatCompletionAsync(effectiveSystemPrompt, userPrompt, resolve.Model, HttpContext.RequestAborted);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
        {
            ErrorMessage = result.Error ?? "LLM response failed.";
            return Page();
        }

        var assistantContent = result.Content.Trim();
        ChatHistory.Add(new ChatMessage("assistant", assistantContent, DateTimeOffset.UtcNow));
        ChatHistoryJson = JsonSerializer.Serialize(ChatHistory);

        await _chatMessages.SaveMessagesAsync(Id, new[]
        {
            new ChatMessageInput("user", userPrompt, DateTimeOffset.UtcNow),
            new ChatMessageInput("assistant", assistantContent, DateTimeOffset.UtcNow)
        }, HttpContext.RequestAborted);
        await _chatMessages.PruneHistoryAsync(Id, HttpContext.RequestAborted);

        UserPrompt = string.Empty;
        SystemPrompt = systemPrompt;

        await LoadPromptCollectionsAsync(preservePromptInputs: true);

        return Page();
    }

    public async Task<IActionResult> OnPostChatAjaxAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return BuildAjaxError("Project not found.", StatusCodes.Status404NotFound);

        var chatScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid chatTenantId || !chatScope.CanUseChat(chatTenantId))
            return Forbid();

        await LoadDashboardAsync(preserveSelection: true, preservePromptInputs: true);

        var userPrompt = NormalizePrompt(UserPrompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
            return BuildAjaxError("Please enter a user prompt.");

        var systemPrompt = ResolveSystemPromptForChat();

        if (!HasLlmConfigured)
            return BuildAjaxError("No LLM model configured. Please update your Profile.");

        if (SelectedDocumentIds.Count == 0)
            return BuildAjaxError("Select at least one document to provide context.");

        if (systemPrompt.Length > SystemPromptMaxLengthChars)
            return BuildAjaxError($"System prompt exceeds the maximum length ({SystemPromptMaxLengthChars} characters).");

        if (userPrompt.Length > UserPromptMaxLengthChars)
            return BuildAjaxError($"User prompt exceeds the maximum length ({UserPromptMaxLengthChars} characters).");

        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return Unauthorized();

        var resolve = await _llmResolver.ResolveAsync(
            userId.Value,
            SelectedModelId,
            ResolveEffectiveDefaultChatModelId(),
            HttpContext.RequestAborted);
        if (!resolve.IsConfigured || resolve.Client is null)
            return BuildAjaxError("Selected LLM model is not configured.");

        var rag = await _ragRetrieval.BuildContextAsync(Id, SelectedDocumentIds, userPrompt, HttpContext.RequestAborted);
        var effectiveSystemPrompt = $"{systemPrompt}\n\nRAG context:\n{rag.Context}";

        ChatHistory = ReadChatHistory();
        ChatHistory.Add(new ChatMessage("user", userPrompt, DateTimeOffset.UtcNow));
        ChatHistoryJson = JsonSerializer.Serialize(ChatHistory);

        try
        {
            await _projectPrompts.SavePromptUsageAsync(Id, ProjectPromptType.System, systemPrompt, HttpContext.RequestAborted);
            await _projectPrompts.SavePromptUsageAsync(Id, ProjectPromptType.User, userPrompt, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            return BuildAjaxError(ex.Message);
        }

        var result = await resolve.Client.ChatCompletionAsync(effectiveSystemPrompt, userPrompt, resolve.Model, HttpContext.RequestAborted);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return BuildAjaxError(result.Error ?? "LLM response failed.");

        var assistantMessage = result.Content.Trim();
        ChatHistory.Add(new ChatMessage("assistant", assistantMessage, DateTimeOffset.UtcNow));
        ChatHistoryJson = JsonSerializer.Serialize(ChatHistory);

        await _chatMessages.SaveMessagesAsync(Id, new[]
        {
            new ChatMessageInput("user", userPrompt, DateTimeOffset.UtcNow),
            new ChatMessageInput("assistant", assistantMessage, DateTimeOffset.UtcNow)
        }, HttpContext.RequestAborted);
        await _chatMessages.PruneHistoryAsync(Id, HttpContext.RequestAborted);

        UserPrompt = string.Empty;
        SystemPrompt = systemPrompt;

        await LoadPromptCollectionsAsync(preservePromptInputs: true);

        return new JsonResult(new
        {
            success = true,
            userMessage = userPrompt,
            assistantMessage,
            chatHistoryJson = ChatHistoryJson,
            promptLibraries = BuildPromptLibrariesPayload()
        });
    }

    public async Task<IActionResult> OnPostChatStreamAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return BuildAjaxError("Project not found.", StatusCodes.Status404NotFound);

        var chatScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid chatTenantId || !chatScope.CanUseChat(chatTenantId))
            return Forbid();

        await LoadDashboardAsync(preserveSelection: true, preservePromptInputs: true);

        var userPrompt = NormalizePrompt(UserPrompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
            return BuildAjaxError("Please enter a user prompt.");

        var systemPrompt = ResolveSystemPromptForChat();

        if (!HasLlmConfigured)
            return BuildAjaxError("No LLM model configured. Please update your Profile.");

        if (SelectedDocumentIds.Count == 0)
            return BuildAjaxError("Select at least one document to provide context.");

        if (systemPrompt.Length > SystemPromptMaxLengthChars)
            return BuildAjaxError($"System prompt exceeds the maximum length ({SystemPromptMaxLengthChars} characters).");

        if (userPrompt.Length > UserPromptMaxLengthChars)
            return BuildAjaxError($"User prompt exceeds the maximum length ({UserPromptMaxLengthChars} characters).");

        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return Unauthorized();

        var resolve = await _llmResolver.ResolveAsync(
            userId.Value,
            SelectedModelId,
            ResolveEffectiveDefaultChatModelId(),
            HttpContext.RequestAborted);
        if (!resolve.IsConfigured || resolve.Client is null)
            return BuildAjaxError("Selected LLM model is not configured.");

        var rag = await _ragRetrieval.BuildContextAsync(Id, SelectedDocumentIds, userPrompt, HttpContext.RequestAborted);
        var effectiveSystemPrompt = $"{systemPrompt}\n\nRAG context:\n{rag.Context}";

        ChatHistory = ReadChatHistory();
        ChatHistory.Add(new ChatMessage("user", userPrompt, DateTimeOffset.UtcNow));
        ChatHistoryJson = JsonSerializer.Serialize(ChatHistory);

        try
        {
            await _projectPrompts.SavePromptUsageAsync(Id, ProjectPromptType.System, systemPrompt, HttpContext.RequestAborted);
            await _projectPrompts.SavePromptUsageAsync(Id, ProjectPromptType.User, userPrompt, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            return BuildAjaxError(ex.Message);
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var assistantBuilder = new StringBuilder();

        try
        {
            await foreach (var chunk in resolve.Client.ChatCompletionStreamAsync(
                               effectiveSystemPrompt,
                               userPrompt,
                               resolve.Model,
                               HttpContext.RequestAborted))
            {
                if (string.IsNullOrEmpty(chunk))
                    continue;

                assistantBuilder.Append(chunk);
                await TryWriteStreamEventAsync(new { type = "token", content = chunk }, HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            await TryWriteStreamEventAsync(new { type = "error", error = ex.Message }, HttpContext.RequestAborted);
            return new EmptyResult();
        }

        var assistantMessage = assistantBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(assistantMessage))
        {
            await TryWriteStreamEventAsync(new { type = "error", error = "LLM response failed." }, HttpContext.RequestAborted);
            return new EmptyResult();
        }

        ChatHistory.Add(new ChatMessage("assistant", assistantMessage, DateTimeOffset.UtcNow));
        ChatHistoryJson = JsonSerializer.Serialize(ChatHistory);

        await _chatMessages.SaveMessagesAsync(Id, new[]
        {
            new ChatMessageInput("user", userPrompt, DateTimeOffset.UtcNow),
            new ChatMessageInput("assistant", assistantMessage, DateTimeOffset.UtcNow)
        }, HttpContext.RequestAborted);
        await _chatMessages.PruneHistoryAsync(Id, HttpContext.RequestAborted);

        UserPrompt = string.Empty;
        SystemPrompt = systemPrompt;

        await LoadPromptCollectionsAsync(preservePromptInputs: true);

        await TryWriteStreamEventAsync(new
        {
            type = "done",
            assistantMessage,
            chatHistoryJson = ChatHistoryJson,
            promptLibraries = BuildPromptLibrariesPayload()
        }, HttpContext.RequestAborted);

        return new EmptyResult();
    }

    public async Task<IActionResult> OnPostSendChatHistoryStreamAsync()
    {
        if (!await EnsureProjectAccessAsync())
            return BuildAjaxError("Project not found.", StatusCodes.Status404NotFound);

        var chatScope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (ProjectTenantId is not Guid chatTenantId || !chatScope.CanUseChat(chatTenantId))
            return Forbid();

        await LoadDashboardAsync(preserveSelection: true, preservePromptInputs: true);

        if (!HasLlmConfigured)
            return BuildAjaxError("No LLM model configured. Please update your Profile.");

        var savedMessages = await _chatMessages.GetHistoryAsync(Id, HttpContext.RequestAborted);
        if (savedMessages.Count == 0)
            return BuildAjaxError("No chat history available to send.");

        var transcript = new StringBuilder();
        foreach (var msg in savedMessages)
        {
            var label = msg.Role == "user" ? "User" : "Assistant";
            var ts = msg.MessageTimestamp.ToString("yyyy-MM-dd HH:mm");
            transcript.AppendLine($"[{label} - {ts}]: {msg.Content}");
        }

        var userId = await ResolveUserIdAsync();
        if (userId is null)
            return Unauthorized();

        var resolve = await _llmResolver.ResolveAsync(
            userId.Value,
            SelectedModelId,
            ResolveEffectiveDefaultChatModelId(),
            HttpContext.RequestAborted);
        if (!resolve.IsConfigured || resolve.Client is null)
            return BuildAjaxError("Selected LLM model is not configured.");

        var systemPrompt = PromptDefaults.ChatHistoryInternalizationPrompt;
        var userMessage = transcript.ToString();

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var assistantBuilder = new StringBuilder();

        try
        {
            await foreach (var chunk in resolve.Client.ChatCompletionStreamAsync(
                               systemPrompt,
                               userMessage,
                               resolve.Model,
                               HttpContext.RequestAborted))
            {
                if (string.IsNullOrEmpty(chunk))
                    continue;

                assistantBuilder.Append(chunk);
                await TryWriteStreamEventAsync(new { type = "token", content = chunk }, HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            await TryWriteStreamEventAsync(new { type = "error", error = ex.Message }, HttpContext.RequestAborted);
            return new EmptyResult();
        }

        var assistantMessage = assistantBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(assistantMessage))
        {
            await TryWriteStreamEventAsync(new { type = "error", error = "LLM response failed." }, HttpContext.RequestAborted);
            return new EmptyResult();
        }

        await _chatMessages.SaveMessagesAsync(Id, new[]
        {
            new ChatMessageInput("assistant", assistantMessage, DateTimeOffset.UtcNow)
        }, HttpContext.RequestAborted);
        await _chatMessages.PruneHistoryAsync(Id, HttpContext.RequestAborted);

        var updatedMessages = await _chatMessages.GetHistoryAsync(Id, HttpContext.RequestAborted);
        ChatHistory = updatedMessages
            .Select(m => new ChatMessage(m.Role, m.Content, m.MessageTimestamp))
            .ToList();
        ChatHistoryJson = JsonSerializer.Serialize(ChatHistory);

        await TryWriteStreamEventAsync(new
        {
            type = "done",
            assistantMessage,
            chatHistoryJson = ChatHistoryJson
        }, HttpContext.RequestAborted);

        return new EmptyResult();
    }

    public async Task<IActionResult> OnPostUsePromptAjaxAsync(Guid promptId, ProjectPromptType promptType)
    {
        if (!await EnsureProjectAccessAsync())
            return BuildAjaxError("Project not found.", StatusCodes.Status404NotFound);

        await LoadDashboardAsync(preserveSelection: true, preservePromptInputs: true);

        var prompt = await _projectPrompts.GetByIdAsync(promptId, HttpContext.RequestAborted);
        if (prompt is null || prompt.ProjectId != Id || prompt.PromptType != promptType)
            return BuildAjaxError("Prompt not found.", StatusCodes.Status404NotFound);

        try
        {
            await _projectPrompts.SavePromptUsageAsync(Id, promptType, prompt.Content, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            return BuildAjaxError(ex.Message);
        }

        await LoadPromptCollectionsAsync(preservePromptInputs: true);

        return new JsonResult(new
        {
            success = true,
            content = prompt.Content,
            promptLibraries = BuildPromptLibrariesPayload()
        });
    }

    public async Task<IActionResult> OnPostTogglePromptFavoriteAjaxAsync(Guid promptId)
    {
        if (!await EnsureProjectAccessAsync())
            return BuildAjaxError("Project not found.", StatusCodes.Status404NotFound);

        await LoadDashboardAsync(preserveSelection: true, preservePromptInputs: true);

        var prompt = await _projectPrompts.GetByIdAsync(promptId, HttpContext.RequestAborted);
        if (prompt is null || prompt.ProjectId != Id)
            return BuildAjaxError("Prompt not found.", StatusCodes.Status404NotFound);

        await _projectPrompts.ToggleFavoriteAsync(promptId, HttpContext.RequestAborted);
        var updatedPrompt = await _projectPrompts.GetByIdAsync(promptId, HttpContext.RequestAborted);
        await LoadPromptCollectionsAsync(preservePromptInputs: true);

        return new JsonResult(new
        {
            success = true,
            isFavorite = updatedPrompt?.IsFavorite ?? false,
            promptLibraries = BuildPromptLibrariesPayload()
        });
    }

    public async Task<IActionResult> OnPostUsePromptAsync(Guid promptId, ProjectPromptType promptType)
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        await LoadDashboardAsync(preserveSelection: true, preservePromptInputs: true);

        var prompt = await _projectPrompts.GetByIdAsync(promptId, HttpContext.RequestAborted);
        if (prompt is null || prompt.ProjectId != Id || prompt.PromptType != promptType)
        {
            ErrorMessage = "Prompt not found.";
            return Page();
        }

        switch (promptType)
        {
            case ProjectPromptType.System:
                SystemPrompt = prompt.Content;
                break;
            case ProjectPromptType.User:
                UserPrompt = prompt.Content;
                break;
        }

        try
        {
            await _projectPrompts.SavePromptUsageAsync(Id, promptType, prompt.Content, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }

        await LoadPromptCollectionsAsync(preservePromptInputs: true);
        return Page();
    }

    public async Task<IActionResult> OnPostTogglePromptFavoriteAsync(Guid promptId)
    {
        if (!await EnsureProjectAccessAsync())
            return RedirectToPage("/Projects/Index");

        await LoadDashboardAsync(preserveSelection: true, preservePromptInputs: true);

        var prompt = await _projectPrompts.GetByIdAsync(promptId, HttpContext.RequestAborted);
        if (prompt is null || prompt.ProjectId != Id)
        {
            ErrorMessage = "Prompt not found.";
            return Page();
        }

        await _projectPrompts.ToggleFavoriteAsync(promptId, HttpContext.RequestAborted);
        await LoadPromptCollectionsAsync(preservePromptInputs: true);
        return Page();
    }

    public string FormatBytes(long bytes)
    {
        const long scale = 1024;
        string[] orders = ["B", "KB", "MB", "GB"];
        double max = bytes;
        int order = 0;
        while (max >= scale && order < orders.Length - 1)
        {
            order++;
            max = max / scale;
        }
        return $"{max:0.##} {orders[order]}";
    }

    private async Task<bool> EnsureProjectAccessAsync()
    {
        var project = await _projects.GetByIdAsync(Id, HttpContext.RequestAborted);
        if (project is null)
            return false;

        var workspace = await _workspaces.GetByIdAsync(project.WorkspaceId, HttpContext.RequestAborted);
        if (workspace is null)
            return false;

        var scope = await _tenantScope.GetScopeAsync(User, HttpContext.RequestAborted);
        if (!scope.IsTenantInScope(workspace.TenantId))
            return false;

        Project = project;
        Workspace = workspace;
        ProjectTenantId = workspace.TenantId;
        return true;
    }

    private async Task LoadDashboardAsync(bool preserveSelection = false, bool preservePromptInputs = false)
    {
        Documents = OrderDocumentsHierarchically(await _documents.GetByProjectAsync(Id, HttpContext.RequestAborted));
        var selectableIds = Documents.Where(IsDocumentSelectable).Select(d => d.Id).ToHashSet();

        if (!preserveSelection)
        {
            SelectedDocumentIds = selectableIds.ToList();
        }
        else
        {
            SelectedDocumentIds = SelectedDocumentIds.Where(selectableIds.Contains).ToList();
        }

        var userId = await ResolveUserIdAsync();
        if (userId is not null)
        {
            var chatUserIds = await ResolveChatModelUserIdsAsync(userId.Value);
            var configured = await _providers.GetConfiguredByUsersAsync(
                chatUserIds,
                LlmProviderType.Chat,
                HttpContext.RequestAborted);
            var effectiveDefaultChatModelId = ResolveEffectiveDefaultChatModelId();
            HasLlmConfigured = configured.Count > 0 || effectiveDefaultChatModelId is not null;

            if (effectiveDefaultChatModelId is not null)
            {
                var sourceLabel = ResolveDefaultModelSourceLabel();
                var modelDisplayName = await ResolveChatModelDisplayNameAsync(effectiveDefaultChatModelId.Value, configured);
                DefaultModelOptionLabel = string.IsNullOrWhiteSpace(modelDisplayName)
                    ? sourceLabel
                    : $"{sourceLabel} ({modelDisplayName})";
            }
            else
            {
                var defaultModel = configured.FirstOrDefault(p => p.IsDefault);
                DefaultModelOptionLabel = defaultModel is null
                    ? "Default model"
                    : $"Default model ({BuildModelOptionLabel(defaultModel)})";
            }

            ModelOptions = configured
                .Where(p => p.Id != effectiveDefaultChatModelId)
                .Select(p => new SelectListItem(
                    BuildModelOptionLabel(p),
                    p.Id.ToString(),
                    SelectedModelId == p.Id)
                {
                    Group = new SelectListGroup { Name = p.GroupName }
                })
                .ToList();
        }
        else
        {
            HasLlmConfigured = false;
            ModelOptions = Array.Empty<SelectListItem>();
            DefaultModelOptionLabel = "Default model";
        }

        await LoadPromptCollectionsAsync(preservePromptInputs);

        var savedMessages = await _chatMessages.GetHistoryAsync(Id, HttpContext.RequestAborted);
        ChatHistory = savedMessages
            .Select(m => new ChatMessage(m.Role, m.Content, m.MessageTimestamp))
            .ToList();
        ChatHistoryJson = JsonSerializer.Serialize(ChatHistory);
    }

    private async Task LoadPromptCollectionsAsync(bool preservePromptInputs)
    {
        var settings = await _platformSettings.GetAsync(HttpContext.RequestAborted);
        SystemPromptMaxLengthChars = settings.SystemPromptMaxLengthChars > 0
            ? settings.SystemPromptMaxLengthChars
            : PromptDefaults.SystemPromptMaxLengthChars;
        UserPromptMaxLengthChars = settings.UserPromptMaxLengthChars > 0
            ? settings.UserPromptMaxLengthChars
            : PromptDefaults.UserPromptMaxLengthChars;
        EffectiveDefaultSystemPrompt = string.IsNullOrWhiteSpace(settings.DefaultSystemPrompt)
            ? PromptDefaults.DefaultSystemPrompt
            : settings.DefaultSystemPrompt;

        SystemPromptCollection = await _projectPrompts.GetProjectPromptsAsync(Id, ProjectPromptType.System, HttpContext.RequestAborted);
        UserPromptCollection = await _projectPrompts.GetProjectPromptsAsync(Id, ProjectPromptType.User, HttpContext.RequestAborted);

        if (preservePromptInputs)
            return;

        var lastSystemPrompt = await _projectPrompts.GetLastUsedAsync(Id, ProjectPromptType.System, HttpContext.RequestAborted);
        SystemPrompt = string.IsNullOrWhiteSpace(lastSystemPrompt?.Content)
            ? EffectiveDefaultSystemPrompt
            : lastSystemPrompt.Content;
        UserPrompt ??= string.Empty;
    }

    private async Task<Guid?> ResolveUserIdAsync()
    {
        return await _users.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            HttpContext.RequestAborted);
    }

    private List<ChatMessage> ReadChatHistory()
    {
        if (string.IsNullOrWhiteSpace(ChatHistoryJson))
            return new List<ChatMessage>();

        try
        {
            return JsonSerializer.Deserialize<List<ChatMessage>>(ChatHistoryJson) ?? new List<ChatMessage>();
        }
        catch
        {
            return new List<ChatMessage>();
        }
    }

    private Guid? ResolveEffectiveDefaultChatModelId()
    {
        if (Project is null)
            return null;

        return Project.ChatLlmOverride switch
        {
            LlmFeatureOverride.Enabled => Project.ChatModelId,
            LlmFeatureOverride.Disabled => null,
            _ => Workspace is not null && Workspace.IsChatLlmEnabled
                ? Workspace.ChatModelId
                : null
        };
    }

    private string ResolveDefaultModelSourceLabel()
    {
        if (Project is null)
            return "Default model";

        return Project.ChatLlmOverride switch
        {
            LlmFeatureOverride.Enabled => "Project default model",
            LlmFeatureOverride.WorkspaceDefault => "Workspace default model",
            _ => "Default model"
        };
    }

    private async Task<string?> ResolveChatModelDisplayNameAsync(
        Guid modelId,
        IReadOnlyList<LlmProviderOptionDto> configuredModels)
    {
        var configured = configuredModels.FirstOrDefault(p => p.Id == modelId);
        if (configured is not null)
            return BuildModelOptionLabel(configured);

        if (ProjectTenantId is not Guid tenantId)
            return null;

        var tenantUsers = await _users.GetByTenantIdAsync(tenantId, HttpContext.RequestAborted);
        var options = await _providers.GetConfiguredByUsersAsync(
            tenantUsers.Select(u => u.Id).ToList(),
            LlmProviderType.Chat,
            HttpContext.RequestAborted);
        var option = options.FirstOrDefault(o => o.Id == modelId);
        if (option is null)
            return null;

        return BuildModelOptionLabel(option);
    }

    private async Task<IReadOnlyList<Guid>> ResolveChatModelUserIdsAsync(Guid currentUserId)
    {
        if (ProjectTenantId is not Guid tenantId)
            return new[] { currentUserId };

        var tenantUsers = await _users.GetByTenantIdAsync(tenantId, HttpContext.RequestAborted);
        var userIds = tenantUsers.Select(u => u.Id).ToHashSet();
        userIds.Add(currentUserId);
        return userIds.ToList();
    }

    private static string BuildModelOptionLabel(LlmProviderOptionDto option)
    {
        var model = string.IsNullOrWhiteSpace(option.Model) ? "default model" : option.Model;
        if (option.Scope == LlmConnectionScope.Hosted)
            return $"Platform - {option.GetName()} ({model})";

        var owner = string.IsNullOrWhiteSpace(option.UserEmail) ? "Personal" : option.UserEmail;
        return $"{owner} - {option.GetName()} ({model})";
    }

    public bool IsDocumentSelectable(DocumentDto document)
        => document.State is not (DocumentState.Quarantined or DocumentState.Rejected);

    public bool CanRetryDocument(DocumentDto document)
        => document.State == DocumentState.Failed;

    private JsonResult BuildAjaxError(string message, int statusCode = StatusCodes.Status400BadRequest)
    {
        return new JsonResult(new { success = false, error = message })
        {
            StatusCode = statusCode
        };
    }

    private async Task TryWriteStreamEventAsync(object payload, CancellationToken cancellationToken)
    {
        try
        {
            await WriteStreamEventAsync(payload, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Ignore disconnections/cancellations while streaming.
        }
        catch (IOException)
        {
            // Ignore client disconnects while streaming.
        }
    }

    private async Task WriteStreamEventAsync(object payload, CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(payload, StreamJsonSerializerOptions);
        await Response.WriteAsync(serialized, cancellationToken);
        await Response.WriteAsync("\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private PromptLibrariesPayload BuildPromptLibrariesPayload()
    {
        return new PromptLibrariesPayload(
            BuildPromptLibraryItems(SystemPromptCollection),
            BuildPromptLibraryItems(UserPromptCollection));
    }

    private static IReadOnlyList<PromptLibraryItemPayload> BuildPromptLibraryItems(ProjectPromptCollectionDto collection)
    {
        var items = new List<PromptLibraryItemPayload>(collection.Favorites.Count + collection.History.Count);

        items.AddRange(collection.Favorites.Select(item => new PromptLibraryItemPayload(
            item.Id,
            item.PromptType,
            true,
            CreatePromptPreview(item.Content, PromptPreviewLength),
            item.LastUsedAt)));

        items.AddRange(collection.History.Select(item => new PromptLibraryItemPayload(
            item.Id,
            item.PromptType,
            false,
            CreatePromptPreview(item.Content, PromptPreviewLength),
            item.LastUsedAt)));

        return items;
    }

    private static IReadOnlyList<ProjectPromptDto> MergePromptCollection(ProjectPromptCollectionDto collection)
    {
        return collection.Favorites
            .Concat(collection.History)
            .ToList();
    }

    private static string CreatePromptPreview(string? content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var singleLine = string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (singleLine.Length <= maxLength)
            return singleLine;

        return $"{singleLine[..maxLength]}...";
    }

    public record ChatMessage(string Role, string Content, DateTimeOffset Timestamp);

    private string ResolveSystemPromptForChat()
    {
        var fromInput = NormalizePrompt(SystemPrompt);
        return string.IsNullOrWhiteSpace(fromInput)
            ? EffectiveDefaultSystemPrompt
            : fromInput;
    }

    private static string NormalizePrompt(string? value) => value?.Trim() ?? string.Empty;

    private static IReadOnlyList<DocumentDto> OrderDocumentsHierarchically(IReadOnlyList<DocumentDto> documents)
    {
        var result = new List<DocumentDto>(documents.Count);
        var childrenByParent = documents
            .Where(d => d.ParentDocumentId is not null)
            .GroupBy(d => d.ParentDocumentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(d => d.FileName).ToList());

        var roots = documents
            .Where(d => d.ParentDocumentId is null)
            .OrderBy(d => d.FileName)
            .ToList();

        void AddWithChildren(DocumentDto doc)
        {
            result.Add(doc);
            if (childrenByParent.TryGetValue(doc.Id, out var children))
                foreach (var child in children)
                    AddWithChildren(child);
        }

        foreach (var root in roots)
            AddWithChildren(root);

        return result;
    }

    private static string NormalizeExtension(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed.ToLowerInvariant() : $".{trimmed.ToLowerInvariant()}";
    }
}
