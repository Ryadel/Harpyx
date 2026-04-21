using Harpyx.Application.Interfaces;
using Harpyx.Application.Telemetry;
using Harpyx.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Harpyx.Worker.Services;

public class JobConsumerService : BackgroundService
{
    private readonly IJobQueue _jobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobConsumerService> _logger;

    public JobConsumerService(
        IJobQueue jobQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<JobConsumerService> logger)
    {
        _jobQueue = jobQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _jobQueue.ConsumeAsync(HandleJobAsync, stoppingToken);
    }

    private async Task HandleJobAsync(Guid documentId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var ragIngestion = scope.ServiceProvider.GetRequiredService<IRagIngestionService>();

        using var activity = HarpyxObservability.JobsActivitySource.StartActivity("Jobs.ParseDocument", System.Diagnostics.ActivityKind.Consumer);
        activity?.SetTag("document.id", documentId);
        activity?.SetTag("job.type", "ParseDocumentJob");

        _logger.LogInformation("Processing ParseDocumentJob for {DocumentId}", documentId);
        var job = new Harpyx.Domain.Entities.Job
        {
            DocumentId = documentId,
            JobType = "ParseDocumentJob",
            State = JobState.Processing,
            AttemptCount = 1
        };

        await jobs.AddAsync(job, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditService.RecordAsync("job_start", null, null, $"ParseDocumentJob started for {documentId}", cancellationToken);

        var document = await documents.GetByIdAsync(documentId, cancellationToken);

        try
        {
            if (document is null)
                throw new InvalidOperationException($"Document {documentId} not found.");

            if (document.State is DocumentState.Quarantined or DocumentState.Rejected)
            {
                job.State = JobState.Failed;
                job.LastError = $"Document {document.Id} is {document.State} and cannot be processed.";
                job.UpdatedAt = DateTimeOffset.UtcNow;
                jobs.Update(job);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await auditService.RecordAsync("job_skipped_security", null, null, $"ParseDocumentJob skipped for {documentId}: state {document.State}", cancellationToken);
                _logger.LogWarning("Skipped ParseDocumentJob for {DocumentId} because document state is {State}", documentId, document.State);
                HarpyxObservability.JobProcessedCounter.Add(1, new("job_type", "ParseDocumentJob"), new("result", "skipped_security"));
                return;
            }

            document.State = DocumentState.Processing;
            document.ExtractionState = DocumentExtractionState.Extracting;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await ragIngestion.IngestDocumentAsync(document, cancellationToken);

            job.State = JobState.Completed;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            jobs.Update(job);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await auditService.RecordAsync("job_end", null, null, $"ParseDocumentJob completed for {documentId}", cancellationToken);
            _logger.LogInformation("Completed ParseDocumentJob for {DocumentId}", documentId);
            HarpyxObservability.JobProcessedCounter.Add(1, new("job_type", "ParseDocumentJob"), new("result", "completed"));
        }
        catch (Exception ex)
        {
            job.State = JobState.Failed;
            job.LastError = ex.Message;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            jobs.Update(job);
            if (document is not null)
            {
                document.State = DocumentState.Failed;
                document.ExtractionState = DocumentExtractionState.Failed;
                document.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await auditService.RecordAsync("job_error", null, null, $"ParseDocumentJob failed for {documentId}: {ex.Message}", cancellationToken);
            _logger.LogError(ex, "ParseDocumentJob failed for {DocumentId}", documentId);
            HarpyxObservability.JobProcessedCounter.Add(1, new("job_type", "ParseDocumentJob"), new("result", "failed"));
            throw;
        }
    }
}
