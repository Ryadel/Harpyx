namespace Harpyx.Domain.Entities;

public class DocumentChunk : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public int ChunkIndex { get; set; }
    public int? PageNumber { get; set; }
    public string SourceType { get; set; } = "text";
    public double? OcrConfidence { get; set; }
    public string Content { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public int IndexVersion { get; set; } = 1;
    public byte[] Embedding { get; set; } = Array.Empty<byte>();
}
