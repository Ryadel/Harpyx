namespace Harpyx.Domain.Entities;

public class PlatformSettings : BaseEntity
{
    public string DefaultSystemPrompt { get; set; } = string.Empty;
    public int SystemPromptMaxLengthChars { get; set; } = 16000;
    public int UserPromptMaxLengthChars { get; set; } = 16000;
    public int SystemPromptHistoryLimitPerProject { get; set; } = 20;
    public int UserPromptHistoryLimitPerProject { get; set; } = 20;
    public bool UserSelfRegistrationEnabled { get; set; } = true;
    public bool QuarantineEnabled { get; set; } = true;
    public bool UrlDocumentsEnabled { get; set; } = true;
    public long MaxFileSizeBytes { get; set; } = 25L * 1024L * 1024L;
    public int ContainerMaxNestingDepth { get; set; } = 3;
    public long ContainerMaxTotalExtractedBytesPerRoot { get; set; } = 500L * 1024L * 1024L;
    public int ContainerMaxFilesPerRoot { get; set; } = 200;
    public long ContainerMaxSingleEntrySizeBytes { get; set; } = 100L * 1024L * 1024L;
    public int RagTopK { get; set; } = 6;
    public int RagMaxContextChars { get; set; } = 10000;
    public int RagRrfK { get; set; } = 60;
    public int RagLexicalCandidateK { get; set; } = 24;
    public int RagVectorCandidateK { get; set; } = 24;
    public int RagKeywordMaxCount { get; set; } = 12;
    public bool RagUseRakeKeywordExtraction { get; set; } = true;
    public int RagContextCacheTtlSeconds { get; set; } = 300;
    public bool RagUseOpenSearchIndexing { get; set; } = true;
    public bool RagUseOpenSearchRetrieval { get; set; } = true;
    public bool RagFallbackToSqlRetrievalOnOpenSearchFailure { get; set; } = true;
    public int ChatHistoryLimitPerProject { get; set; } = 30;
}
