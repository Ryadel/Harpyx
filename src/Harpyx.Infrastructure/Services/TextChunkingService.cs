namespace Harpyx.Infrastructure.Services;

public interface ITextChunkingService
{
    IReadOnlyList<ChunkCandidate> BuildChunks(IReadOnlyList<ExtractedPageText> pages);
}

public record ChunkCandidate(int ChunkIndex, int? PageNumber, string SourceType, double? OcrConfidence, string Content);

public class TextChunkingService : ITextChunkingService
{
    private readonly RagOptions _options;

    public TextChunkingService(RagOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<ChunkCandidate> BuildChunks(IReadOnlyList<ExtractedPageText> pages)
    {
        var chunks = new List<ChunkCandidate>();
        if (pages.Count == 0)
            return chunks;

        var chunkSize = Math.Max(200, _options.ChunkSizeChars);
        var overlap = Math.Clamp(_options.ChunkOverlapChars, 0, chunkSize - 1);
        var stride = Math.Max(1, chunkSize - overlap);
        var chunkIndex = 0;

        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text))
                continue;

            var start = 0;
            while (start < page.Text.Length)
            {
                var length = Math.Min(chunkSize, page.Text.Length - start);
                var content = page.Text.Substring(start, length).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    chunks.Add(new ChunkCandidate(
                        chunkIndex++,
                        page.PageNumber,
                        page.SourceType,
                        page.OcrConfidence,
                        content));
                }

                if (start + length >= page.Text.Length)
                    break;

                start += stride;
            }
        }

        return chunks;
    }
}
