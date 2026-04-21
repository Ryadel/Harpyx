namespace Harpyx.Shared;

public static class HarpyxConstants
{
    public const string JobQueueName = "harpyx.jobs";
    public const string DeadLetterQueueName = "harpyx.jobs.dlq";
    public const string MinioBucketName = "harpyx-documents";
    public const string IdempotencyPrefix = "harpyx:idempotency:";
}
