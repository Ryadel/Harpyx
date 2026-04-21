namespace Harpyx.Infrastructure.Services;

public class RagOptions
{
    public int ChunkSizeChars { get; set; } = 1400;
    public int ChunkOverlapChars { get; set; } = 250;
    public int PdfTextMinCharsBeforeOcr { get; set; } = 200;
    public string OcrLanguages { get; set; } = "ita+eng";
    public string OpenAiEmbeddingModel { get; set; } = "text-embedding-3-small";
    public string GoogleEmbeddingModel { get; set; } = "gemini-embedding-001";
    public int EmbeddingBatchSize { get; set; } = 32;
}
