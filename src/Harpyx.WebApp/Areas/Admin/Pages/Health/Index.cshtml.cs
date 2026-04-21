using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Harpyx.WebApp.Areas.Admin.Pages.Health;

public class IndexModel : PageModel
{
    private static readonly IReadOnlyDictionary<string, HealthCheckMetadata> Metadata = new Dictionary<string, HealthCheckMetadata>(StringComparer.OrdinalIgnoreCase)
    {
        ["self"] = new("Web Process", "Core", HealthDependencyImportance.Required, "Incoming requests can reach the Harpyx web process."),
        ["sql"] = new("SQL Server", "Core", HealthDependencyImportance.Required, "Users, tenants, projects, documents, settings, and audit state."),
        ["redis"] = new("Redis", "Core", HealthDependencyImportance.Required, "Idempotency tokens and short-lived runtime caches."),
        ["rabbitmq"] = new("RabbitMQ", "Jobs", HealthDependencyImportance.Required, "Document ingestion, parsing, retries, and worker handoff."),
        ["minio"] = new("MinIO", "Storage", HealthDependencyImportance.Required, "Uploaded document binaries and extracted artifacts."),
        ["opensearch"] = new("OpenSearch", "Search", HealthDependencyImportance.Optional, "Hybrid retrieval and RAG search indexes."),
        ["clamav"] = new("ClamAV", "Security", HealthDependencyImportance.Optional, "Upload malware scanning and quarantine decisions.")
    };

    private readonly HealthCheckService _healthCheckService;

    public IndexModel(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    public IReadOnlyList<DependencyHealthItem> Dependencies { get; private set; } = Array.Empty<DependencyHealthItem>();

    public DateTimeOffset CheckedAtUtc { get; private set; }

    public TimeSpan TotalDuration { get; private set; }

    public int RequiredUnavailableCount => Dependencies.Count(item =>
        item.Importance == HealthDependencyImportance.Required && item.DisplayStatus is DependencyDisplayStatus.Down or DependencyDisplayStatus.Degraded);

    public int OptionalUnavailableCount => Dependencies.Count(item =>
        item.Importance == HealthDependencyImportance.Optional && item.DisplayStatus is DependencyDisplayStatus.Down or DependencyDisplayStatus.Degraded);

    public int DisabledCount => Dependencies.Count(item => item.DisplayStatus == DependencyDisplayStatus.Disabled);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CheckedAtUtc = DateTimeOffset.UtcNow;
        var report = await _healthCheckService.CheckHealthAsync(_ => true, cancellationToken);
        TotalDuration = report.TotalDuration;

        Dependencies = report.Entries
            .Select(entry => CreateItem(entry.Key, entry.Value))
            .OrderBy(item => item.Importance == HealthDependencyImportance.Required ? 0 : 1)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.DisplayName)
            .ToList();
    }

    private static DependencyHealthItem CreateItem(string key, HealthReportEntry entry)
    {
        var metadata = Metadata.TryGetValue(key, out var configuredMetadata)
            ? configuredMetadata
            : new HealthCheckMetadata(key, "Other", HealthDependencyImportance.Optional, "No operational impact metadata configured.");

        var displayStatus = ResolveDisplayStatus(metadata.Importance, entry);
        return new DependencyHealthItem(
            Key: key,
            DisplayName: metadata.DisplayName,
            Category: metadata.Category,
            Importance: metadata.Importance,
            Impact: metadata.Impact,
            DisplayStatus: displayStatus,
            HealthStatus: entry.Status,
            Description: entry.Description,
            Error: entry.Exception?.Message,
            Duration: entry.Duration);
    }

    private static DependencyDisplayStatus ResolveDisplayStatus(HealthDependencyImportance importance, HealthReportEntry entry)
    {
        if (importance == HealthDependencyImportance.Optional &&
            entry.Status == HealthStatus.Healthy &&
            entry.Description?.Contains("disabled", StringComparison.OrdinalIgnoreCase) == true)
        {
            return DependencyDisplayStatus.Disabled;
        }

        return entry.Status switch
        {
            HealthStatus.Healthy => DependencyDisplayStatus.Up,
            HealthStatus.Degraded => DependencyDisplayStatus.Degraded,
            _ => DependencyDisplayStatus.Down
        };
    }
}

public sealed record DependencyHealthItem(
    string Key,
    string DisplayName,
    string Category,
    HealthDependencyImportance Importance,
    string Impact,
    DependencyDisplayStatus DisplayStatus,
    HealthStatus HealthStatus,
    string? Description,
    string? Error,
    TimeSpan Duration)
{
    public string StatusLabel => DisplayStatus switch
    {
        DependencyDisplayStatus.Up => "Up",
        DependencyDisplayStatus.Disabled => "Disabled",
        DependencyDisplayStatus.Degraded => "Degraded",
        _ => "Down"
    };

    public string StatusBadgeCss => DisplayStatus switch
    {
        DependencyDisplayStatus.Up => "badge-success",
        DependencyDisplayStatus.Disabled => "badge-ghost",
        DependencyDisplayStatus.Degraded => "badge-warning",
        _ => "badge-error"
    };

    public string ImportanceLabel => Importance == HealthDependencyImportance.Required ? "Required" : "Optional";

    public string ImportanceBadgeCss => Importance == HealthDependencyImportance.Required ? "badge-primary" : "badge-secondary";

    public string DurationLabel => $"{Duration.TotalMilliseconds:0.##} ms";
}

public sealed record HealthCheckMetadata(
    string DisplayName,
    string Category,
    HealthDependencyImportance Importance,
    string Impact);

public enum HealthDependencyImportance
{
    Required,
    Optional
}

public enum DependencyDisplayStatus
{
    Up,
    Disabled,
    Degraded,
    Down
}
