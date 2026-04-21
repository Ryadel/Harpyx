namespace Harpyx.Domain.Enums;

public enum DocumentState
{
    Uploaded = 0,
    Queued = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Quarantined = 5,
    Rejected = 6
}
