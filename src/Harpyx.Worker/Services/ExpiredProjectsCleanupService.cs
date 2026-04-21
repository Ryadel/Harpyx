using Harpyx.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Harpyx.Worker.Services;

public class ExpiredProjectsCleanupService : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredProjectsCleanupService> _logger;

    public ExpiredProjectsCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredProjectsCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deletedCount = await CleanupExpiredProjectsAsync(stoppingToken);
                if (deletedCount > 0)
                {
                    _logger.LogInformation("Expired project cleanup removed {Count} projects.", deletedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expired project cleanup failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<int> CleanupExpiredProjectsAsync(CancellationToken cancellationToken)
    {
        var deleted = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();

            var expiredProjectIds = await projects.GetExpiredProjectIdsAsync(
                DateTimeOffset.UtcNow,
                BatchSize,
                cancellationToken);

            if (expiredProjectIds.Count == 0)
                break;

            foreach (var projectId in expiredProjectIds)
            {
                try
                {
                    await projectService.DeleteAsync(projectId, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete expired project {ProjectId}.", projectId);
                }
            }

            if (expiredProjectIds.Count < BatchSize)
                break;
        }

        return deleted;
    }
}
