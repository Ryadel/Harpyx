using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Harpyx.Application.Telemetry;

public static class HarpyxObservability
{
    public const string UsageLimitsActivitySourceName = "Harpyx.UsageLimits";
    public const string JobsActivitySourceName = "Harpyx.Jobs";
    public const string WebhooksActivitySourceName = "Harpyx.Webhooks";

    public const string UsageLimitsMeterName = "Harpyx.UsageLimits";
    public const string JobsMeterName = "Harpyx.Jobs";
    public const string WebhooksMeterName = "Harpyx.Webhooks";

    public static readonly ActivitySource UsageLimitsActivitySource = new(UsageLimitsActivitySourceName);
    public static readonly ActivitySource JobsActivitySource = new(JobsActivitySourceName);
    public static readonly ActivitySource WebhooksActivitySource = new(WebhooksActivitySourceName);

    public static readonly Meter UsageLimitsMeter = new(UsageLimitsMeterName);
    public static readonly Meter JobsMeter = new(JobsMeterName);
    public static readonly Meter WebhooksMeter = new(WebhooksMeterName);

    public static readonly Counter<long> UsageLimitOperationsCounter =
        UsageLimitsMeter.CreateCounter<long>("harpyx.usage_limits.operations", unit: "{operation}", description: "Usage limit operations.");

    public static readonly Counter<long> JobProcessedCounter =
        JobsMeter.CreateCounter<long>("harpyx.jobs.processed", unit: "{job}", description: "Background jobs processed.");

    public static readonly Counter<long> WebhookRequestsCounter =
        WebhooksMeter.CreateCounter<long>("harpyx.webhooks.requests", unit: "{request}", description: "Webhook requests processed.");
}
