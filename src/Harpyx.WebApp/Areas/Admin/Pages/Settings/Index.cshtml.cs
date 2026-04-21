using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Harpyx.WebApp.Areas.Admin.Pages.Settings;

public class IndexModel : PageModel
{
    private readonly IPlatformSettingsService _settings;

    public IndexModel(IPlatformSettingsService settings)
    {
        _settings = settings;
    }

    [BindProperty]
    [StringLength(100000)]
    public string? DefaultSystemPrompt { get; set; }

    [BindProperty]
    [Range(1, 100000)]
    public int SystemPromptMaxLengthChars { get; set; }

    [BindProperty]
    [Range(1, 100000)]
    public int UserPromptMaxLengthChars { get; set; }

    [BindProperty]
    [Range(0, 200)]
    public int SystemPromptHistoryLimitPerProject { get; set; }

    [BindProperty]
    [Range(0, 200)]
    public int UserPromptHistoryLimitPerProject { get; set; }

    [BindProperty]
    [Range(0, 500)]
    public int ChatHistoryLimitPerProject { get; set; }

    [BindProperty]
    public bool UserSelfRegistrationEnabled { get; set; }

    [BindProperty]
    public bool QuarantineEnabled { get; set; }

    [BindProperty]
    public bool UrlDocumentsEnabled { get; set; }

    [BindProperty]
    [Range(1048576, 1073741824)]
    public long MaxFileSizeBytes { get; set; }

    [BindProperty]
    [Range(1, 10)]
    public int ContainerMaxNestingDepth { get; set; }

    [BindProperty]
    [Range(26214400L, 10737418240L)]
    public long ContainerMaxTotalExtractedBytesPerRoot { get; set; }

    [BindProperty]
    [Range(1, 10000)]
    public int ContainerMaxFilesPerRoot { get; set; }

    [BindProperty]
    [Range(1048576L, 2147483648L)]
    public long ContainerMaxSingleEntrySizeBytes { get; set; }

    [BindProperty]
    [Range(1, 30)]
    public int RagTopK { get; set; }

    [BindProperty]
    [Range(2000, 50000)]
    public int RagMaxContextChars { get; set; }

    [BindProperty]
    [Range(1, 500)]
    public int RagRrfK { get; set; }

    [BindProperty]
    [Range(1, 200)]
    public int RagLexicalCandidateK { get; set; }

    [BindProperty]
    [Range(1, 200)]
    public int RagVectorCandidateK { get; set; }

    [BindProperty]
    [Range(1, 100)]
    public int RagKeywordMaxCount { get; set; }

    [BindProperty]
    public bool RagUseRakeKeywordExtraction { get; set; }

    [BindProperty]
    [Range(5, 3600)]
    public int RagContextCacheTtlSeconds { get; set; }

    [BindProperty]
    public bool RagUseOpenSearchIndexing { get; set; }

    [BindProperty]
    public bool RagUseOpenSearchRetrieval { get; set; }

    [BindProperty]
    public bool RagFallbackToSqlRetrievalOnOpenSearchFailure { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        var current = await _settings.GetAsync(HttpContext.RequestAborted);
        DefaultSystemPrompt = current.DefaultSystemPrompt;
        SystemPromptMaxLengthChars = current.SystemPromptMaxLengthChars;
        UserPromptMaxLengthChars = current.UserPromptMaxLengthChars;
        SystemPromptHistoryLimitPerProject = current.SystemPromptHistoryLimitPerProject;
        UserPromptHistoryLimitPerProject = current.UserPromptHistoryLimitPerProject;
        ChatHistoryLimitPerProject = current.ChatHistoryLimitPerProject;
        UserSelfRegistrationEnabled = current.UserSelfRegistrationEnabled;
        QuarantineEnabled = current.QuarantineEnabled;
        UrlDocumentsEnabled = current.UrlDocumentsEnabled;
        MaxFileSizeBytes = current.MaxFileSizeBytes;
        ContainerMaxNestingDepth = current.ContainerMaxNestingDepth;
        ContainerMaxTotalExtractedBytesPerRoot = current.ContainerMaxTotalExtractedBytesPerRoot;
        ContainerMaxFilesPerRoot = current.ContainerMaxFilesPerRoot;
        ContainerMaxSingleEntrySizeBytes = current.ContainerMaxSingleEntrySizeBytes;
        RagTopK = current.RagTopK;
        RagMaxContextChars = current.RagMaxContextChars;
        RagRrfK = current.RagRrfK;
        RagLexicalCandidateK = current.RagLexicalCandidateK;
        RagVectorCandidateK = current.RagVectorCandidateK;
        RagKeywordMaxCount = current.RagKeywordMaxCount;
        RagUseRakeKeywordExtraction = current.RagUseRakeKeywordExtraction;
        RagContextCacheTtlSeconds = current.RagContextCacheTtlSeconds;
        RagUseOpenSearchIndexing = current.RagUseOpenSearchIndexing;
        RagUseOpenSearchRetrieval = current.RagUseOpenSearchRetrieval;
        RagFallbackToSqlRetrievalOnOpenSearchFailure = current.RagFallbackToSqlRetrievalOnOpenSearchFailure;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _settings.SaveAsync(
            new PlatformSettingsSaveRequest(
                DefaultSystemPrompt,
                SystemPromptMaxLengthChars,
                UserPromptMaxLengthChars,
                SystemPromptHistoryLimitPerProject,
                UserPromptHistoryLimitPerProject,
                ChatHistoryLimitPerProject,
                UserSelfRegistrationEnabled,
                QuarantineEnabled,
                UrlDocumentsEnabled,
                MaxFileSizeBytes,
                ContainerMaxNestingDepth,
                ContainerMaxTotalExtractedBytesPerRoot,
                ContainerMaxFilesPerRoot,
                ContainerMaxSingleEntrySizeBytes,
                RagTopK,
                RagMaxContextChars,
                RagRrfK,
                RagLexicalCandidateK,
                RagVectorCandidateK,
                RagKeywordMaxCount,
                RagUseRakeKeywordExtraction,
                RagContextCacheTtlSeconds,
                RagUseOpenSearchIndexing,
                RagUseOpenSearchRetrieval,
                RagFallbackToSqlRetrievalOnOpenSearchFailure),
            HttpContext.RequestAborted);

        SuccessMessage = "Platform settings saved.";
        return RedirectToPage("/Settings/Index", new { area = "Admin" });
    }
}
