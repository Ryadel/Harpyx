using Harpyx.Application.Defaults;
using Harpyx.Application.DTOs;
using Harpyx.Application.Exceptions;
using Harpyx.Application.Interfaces;
using Harpyx.Application.Telemetry;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Harpyx.Application.Services;

public class UsageLimitService : IUsageLimitService
{
    private const long BytesPerGigabyte = 1024L * 1024L * 1024L;

    private readonly IPlatformUsageLimitsRepository _limits;
    private readonly IUsageMetricsRepository _usage;
    private readonly IWorkspaceRepository _workspaces;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UsageLimitService> _logger;

    public UsageLimitService(
        IPlatformUsageLimitsRepository limits,
        IUsageMetricsRepository usage,
        IWorkspaceRepository workspaces,
        IUnitOfWork unitOfWork,
        ILogger<UsageLimitService>? logger = null)
    {
        _limits = limits;
        _usage = usage;
        _workspaces = workspaces;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<UsageLimitService>.Instance;
    }

    public async Task<UsageLimitsDto> GetAsync(CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<UsageLimitsDto> SaveAsync(UsageLimitsSaveRequest request, CancellationToken cancellationToken)
    {
        using var activity = HarpyxObservability.UsageLimitsActivitySource.StartActivity("UsageLimits.Save");
        Validate(request);

        var entity = await GetOrCreateAsync(cancellationToken);
        entity.TenantsPerUser = Normalize(request.TenantsPerUser);
        entity.WorkspacesPerUser = Normalize(request.WorkspacesPerUser);
        entity.DocumentsPerWorkspace = Normalize(request.DocumentsPerWorkspace);
        entity.StoragePerUserGb = Normalize(request.StoragePerUserGb);
        entity.StoragePerTenantGb = Normalize(request.StoragePerTenantGb);
        entity.StoragePerWorkspaceGb = Normalize(request.StoragePerWorkspaceGb);
        entity.ProjectsPerWorkspace = Normalize(request.ProjectsPerWorkspace);
        entity.PermanentProjectsPerWorkspace = Normalize(request.PermanentProjectsPerWorkspace);
        entity.MaxTemporaryProjectLifetimeHours = Normalize(request.MaxTemporaryProjectLifetimeHours);
        entity.LlmProvidersPerUser = Normalize(request.LlmProvidersPerUser);
        entity.EnableOcr = request.EnableOcr;
        entity.EnableRagIndexing = request.EnableRagIndexing;
        entity.EnableApi = request.EnableApi;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        _limits.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        HarpyxObservability.UsageLimitOperationsCounter.Add(
            1,
            new KeyValuePair<string, object?>("operation", "save_usage_limits"));
        _logger.LogInformation("Platform usage limits updated.");

        return ToDto(entity);
    }

    public async Task EnsureTenantCreationAllowedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var limits = await GetOrCreateAsync(cancellationToken);
        if (limits.TenantsPerUser is not int tenantLimit)
            return;

        var current = await _usage.CountTenantsByUserAsync(userId, cancellationToken);
        if (current >= tenantLimit)
        {
            throw new UsageLimitExceededException(
                $"Tenant limit reached for this instance ({tenantLimit}).");
        }
    }

    public async Task EnsureWorkspaceCreationAllowedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var limits = await GetOrCreateAsync(cancellationToken);
        if (limits.WorkspacesPerUser is not int workspaceLimit)
            return;

        var current = await _usage.CountWorkspacesByUserAsync(userId, cancellationToken);
        if (current >= workspaceLimit)
        {
            throw new UsageLimitExceededException(
                $"Workspace limit reached for this instance ({workspaceLimit}).");
        }
    }

    public async Task EnsureProjectCreationAllowedAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await _workspaces.GetByIdAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("Workspace not found.");

        var limits = await GetOrCreateAsync(cancellationToken);
        if (limits.ProjectsPerWorkspace is not int projectLimit)
            return;

        var current = await _usage.CountProjectsByWorkspaceAsync(workspace.Id, cancellationToken);
        if (current >= projectLimit)
        {
            throw new UsageLimitExceededException(
                $"Project limit reached for this workspace ({projectLimit}).");
        }
    }

    public async Task EnsureProjectLifetimeAllowedAsync(
        Guid workspaceId,
        Guid? projectId,
        ProjectLifetimePreset? lifetimePreset,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaces.GetByIdAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("Workspace not found.");

        var limits = await GetOrCreateAsync(cancellationToken);

        if (lifetimePreset is null)
        {
            if (limits.PermanentProjectsPerWorkspace is not int permanentLimit)
                return;

            var currentPermanent = await _usage.CountPermanentProjectsByWorkspaceAsync(workspace.Id, projectId, cancellationToken);
            if (currentPermanent >= permanentLimit)
            {
                throw new UsageLimitExceededException(
                    $"Permanent project limit reached for this workspace ({permanentLimit}).");
            }

            return;
        }

        if (limits.MaxTemporaryProjectLifetimeHours is not int maxTemporaryHours)
            return;

        var requestedHours = ProjectLifetimeDefaults.GetDurationHours(lifetimePreset.Value);
        if (requestedHours > maxTemporaryHours)
        {
            throw new UsageLimitExceededException(
                $"Selected project lifetime exceeds the configured instance limit ({maxTemporaryHours} hours max).");
        }
    }

    public async Task EnsureLlmProviderCreationAllowedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var limits = await GetOrCreateAsync(cancellationToken);
        if (limits.LlmProvidersPerUser is not int providerLimit)
            return;

        var current = await _usage.CountLlmProvidersByUserAsync(userId, cancellationToken);
        if (current >= providerLimit)
        {
            throw new UsageLimitExceededException(
                $"LLM provider limit reached for this instance ({providerLimit}).");
        }
    }

    public async Task EnsureDocumentUploadAllowedAsync(Guid userId, Guid tenantId, Guid workspaceId, long uploadSizeBytes, CancellationToken cancellationToken)
    {
        if (uploadSizeBytes <= 0)
            return;

        var limits = await GetOrCreateAsync(cancellationToken);

        if (limits.DocumentsPerWorkspace is int documentsPerWorkspace)
        {
            var currentDocuments = await _usage.CountDocumentsByWorkspaceAsync(workspaceId, cancellationToken);
            if (currentDocuments >= documentsPerWorkspace)
            {
                throw new UsageLimitExceededException(
                    $"Document limit reached for this workspace ({documentsPerWorkspace}).");
            }
        }

        await EnsureStorageLimitAsync(
            limits.StoragePerWorkspaceGb,
            await _usage.GetStorageByWorkspaceAsync(workspaceId, cancellationToken),
            uploadSizeBytes,
            "workspace");

        await EnsureStorageLimitAsync(
            limits.StoragePerTenantGb,
            await _usage.GetStorageByTenantAsync(tenantId, cancellationToken),
            uploadSizeBytes,
            "tenant");

        await EnsureStorageLimitAsync(
            limits.StoragePerUserGb,
            await _usage.GetStorageByUserAsync(userId, cancellationToken),
            uploadSizeBytes,
            "user");
    }

    public async Task EnsureRagIndexingAllowedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var limits = await GetOrCreateAsync(cancellationToken);
        if (limits.EnableRagIndexing)
            return;

        throw new UsageLimitExceededException("RAG indexing is disabled for this instance.");
    }

    public async Task<bool> IsOcrAllowedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var limits = await GetOrCreateAsync(cancellationToken);
        return limits.EnableOcr;
    }

    public async Task<bool> IsApiEnabledForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var limits = await GetOrCreateAsync(cancellationToken);
        return limits.EnableApi;
    }

    public async Task EnsureApiAccessAllowedAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await IsApiEnabledForUserAsync(userId, cancellationToken))
            return;

        throw new UsageLimitExceededException("API access is disabled for this instance.");
    }

    private async Task EnsureStorageLimitAsync(int? limitGb, long currentBytes, long uploadSizeBytes, string scopeName)
    {
        if (limitGb is null)
            return;

        var maxBytes = limitGb.Value * BytesPerGigabyte;
        var nextBytes = currentBytes + uploadSizeBytes;
        if (nextBytes > maxBytes)
        {
            throw new UsageLimitExceededException(
                $"Storage limit exceeded for {scopeName}. Maximum allowed: {limitGb.Value} GB.");
        }

        await Task.CompletedTask;
    }

    private async Task<PlatformUsageLimits> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var entity = await _limits.GetAsync(cancellationToken);
        if (entity is not null)
            return entity;

        entity = new PlatformUsageLimits();
        await _limits.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private static void Validate(UsageLimitsSaveRequest request)
    {
        ValidateNonNegative(request.TenantsPerUser, nameof(request.TenantsPerUser));
        ValidateNonNegative(request.WorkspacesPerUser, nameof(request.WorkspacesPerUser));
        ValidateNonNegative(request.DocumentsPerWorkspace, nameof(request.DocumentsPerWorkspace));
        ValidateNonNegative(request.StoragePerUserGb, nameof(request.StoragePerUserGb));
        ValidateNonNegative(request.StoragePerTenantGb, nameof(request.StoragePerTenantGb));
        ValidateNonNegative(request.StoragePerWorkspaceGb, nameof(request.StoragePerWorkspaceGb));
        ValidateNonNegative(request.ProjectsPerWorkspace, nameof(request.ProjectsPerWorkspace));
        ValidateNonNegative(request.PermanentProjectsPerWorkspace, nameof(request.PermanentProjectsPerWorkspace));
        ValidateNonNegative(request.MaxTemporaryProjectLifetimeHours, nameof(request.MaxTemporaryProjectLifetimeHours));
        ValidateNonNegative(request.LlmProvidersPerUser, nameof(request.LlmProvidersPerUser));
    }

    private static void ValidateNonNegative(int? value, string fieldName)
    {
        if (value is not null && value.Value < 0)
            throw new InvalidOperationException($"{fieldName} cannot be negative.");
    }

    private static int? Normalize(int? value) => value is not null && value.Value < 0 ? 0 : value;

    private static UsageLimitsDto ToDto(PlatformUsageLimits entity)
    {
        return new UsageLimitsDto(
            entity.TenantsPerUser,
            entity.WorkspacesPerUser,
            entity.DocumentsPerWorkspace,
            entity.StoragePerUserGb,
            entity.StoragePerTenantGb,
            entity.StoragePerWorkspaceGb,
            entity.ProjectsPerWorkspace,
            entity.PermanentProjectsPerWorkspace,
            entity.MaxTemporaryProjectLifetimeHours,
            entity.LlmProvidersPerUser,
            entity.EnableOcr,
            entity.EnableRagIndexing,
            entity.EnableApi);
    }
}
