using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class Document : BaseEntity
{
    public Guid? UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public int Version { get; set; } = 1;
    public DocumentState State { get; set; } = DocumentState.Uploaded;
    public Guid? ParentDocumentId { get; set; }
    public Document? ParentDocument { get; set; }
    public ICollection<Document> ChildDocuments { get; set; } = new List<Document>();
    public Guid? RootContainerDocumentId { get; set; }
    public Document? RootContainerDocument { get; set; }
    public int NestingLevel { get; set; } = 0;
    public string? ContainerPath { get; set; }
    public bool IsContainer { get; set; } = false;
    public DocumentContainerType ContainerType { get; set; } = DocumentContainerType.None;
    public DocumentExtractionState ExtractionState { get; set; } = DocumentExtractionState.Pending;
    public Guid? OriginatingUploadId { get; set; }
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
