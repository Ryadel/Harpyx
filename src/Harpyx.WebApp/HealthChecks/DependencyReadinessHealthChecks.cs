using Harpyx.Infrastructure.Services;
using Harpyx.Shared;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using Minio.DataModel.Args;
using nClam;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Harpyx.WebApp.HealthChecks;

public sealed class SqlServerReadinessHealthCheck : IHealthCheck
{
    private readonly HarpyxDbContext _dbContext;

    public SqlServerReadinessHealthCheck(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("SQL reachable.")
            : HealthCheckResult.Unhealthy("SQL not reachable.");
    }
}

public sealed class RedisReadinessHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisReadinessHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected)
            return HealthCheckResult.Unhealthy("Redis multiplexer not connected.");

        var latency = await _redis.GetDatabase().PingAsync();
        return HealthCheckResult.Healthy($"Redis reachable. Ping={latency.TotalMilliseconds:0.##}ms.");
    }
}

public sealed class OpenSearchReadinessHealthCheck : IHealthCheck
{
    private readonly OpenSearchOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenSearchReadinessHealthCheck(OpenSearchOptions options, IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return HealthCheckResult.Healthy("OpenSearch disabled.");

        try
        {
            var client = _httpClientFactory.CreateClient("OpenSearch");
            using var request = new HttpRequestMessage(HttpMethod.Get, "/_cluster/health?wait_for_status=yellow&timeout=2s");
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                    $"{_options.Username}:{_options.Password ?? string.Empty}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return HealthCheckResult.Unhealthy($"OpenSearch not ready: {(int)response.StatusCode} {body}");
            }

            return HealthCheckResult.Healthy("OpenSearch reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("OpenSearch not reachable.", ex);
        }
    }
}

public sealed class RabbitMqReadinessHealthCheck : IHealthCheck
{
    private readonly RabbitMqOptions _options;

    public RabbitMqReadinessHealthCheck(RabbitMqOptions options)
    {
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                UserName = _options.UserName,
                Password = _options.Password,
                AutomaticRecoveryEnabled = false,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                SocketReadTimeout = TimeSpan.FromSeconds(5),
                SocketWriteTimeout = TimeSpan.FromSeconds(5)
            };

            using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ reachable.")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ not reachable.", ex);
        }
    }
}

public sealed class MinioReadinessHealthCheck : IHealthCheck
{
    private readonly IMinioClient _client;

    public MinioReadinessHealthCheck(IMinioClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(HarpyxConstants.MinioBucketName),
                cancellationToken);

            var message = bucketExists
                ? $"MinIO reachable. Bucket '{HarpyxConstants.MinioBucketName}' exists."
                : $"MinIO reachable. Bucket '{HarpyxConstants.MinioBucketName}' does not exist yet.";

            return HealthCheckResult.Healthy(message);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO not reachable.", ex);
        }
    }
}

public sealed class ClamAvReadinessHealthCheck : IHealthCheck
{
    private readonly MalwareScanOptions _options;

    public ClamAvReadinessHealthCheck(MalwareScanOptions options)
    {
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return HealthCheckResult.Healthy("ClamAV scan disabled.");

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

            var client = new ClamClient(_options.Host, _options.Port);
            var alive = await client.PingAsync(timeoutCts.Token);
            return alive
                ? HealthCheckResult.Healthy("ClamAV reachable.")
                : HealthCheckResult.Unhealthy("ClamAV ping failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("ClamAV not reachable.", ex);
        }
    }
}

public sealed class HealthAlertPublisher : IHealthCheckPublisher
{
    private readonly ILogger<HealthAlertPublisher> _logger;
    private readonly Dictionary<string, HealthStatus> _lastStatuses = new(StringComparer.OrdinalIgnoreCase);

    public HealthAlertPublisher(ILogger<HealthAlertPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var (name, entry) in report.Entries)
        {
            var previous = _lastStatuses.TryGetValue(name, out var lastStatus) ? lastStatus : HealthStatus.Healthy;
            _lastStatuses[name] = entry.Status;

            if (entry.Status == previous)
                continue;

            if (entry.Status == HealthStatus.Healthy)
            {
                _logger.LogInformation(
                    "Health check recovered. Check={Check}, PreviousStatus={PreviousStatus}, CurrentStatus={CurrentStatus}",
                    name,
                    previous,
                    entry.Status);
                continue;
            }

            _logger.LogWarning(
                entry.Exception,
                "Health check degraded. Check={Check}, PreviousStatus={PreviousStatus}, CurrentStatus={CurrentStatus}, Description={Description}",
                name,
                previous,
                entry.Status,
                entry.Description);
        }

        if (report.Status != HealthStatus.Healthy)
        {
            _logger.LogError("Readiness report is not healthy. Status={Status}", report.Status);
        }

        return Task.CompletedTask;
    }
}
