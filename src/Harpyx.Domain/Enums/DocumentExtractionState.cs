namespace Harpyx.Domain.Enums;

public enum DocumentExtractionState
{
    Pending = 0,
    Extracting = 1,
    Completed = 2,
    CompletedWithWarnings = 3,
    Failed = 4,
    Unsupported = 5,
    BlockedByPolicy = 6
}
