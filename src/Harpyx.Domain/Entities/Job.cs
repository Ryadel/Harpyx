using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class Job : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public string JobType { get; set; } = "ParseDocumentJob";
    public JobState State { get; set; } = JobState.Queued;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
