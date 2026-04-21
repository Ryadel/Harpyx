using Harpyx.Application.DTOs;
using Harpyx.Application.Exceptions;

namespace Harpyx.WebApp.UnitTests;

public class UsageLimitServiceTests
{
    [Fact]
    public async Task GetAsync_WhenLimitsMissing_SeedsDefaults()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var workspaces = new Mock<IWorkspaceRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        PlatformUsageLimits? added = null;

        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlatformUsageLimits?)null);
        limits.Setup(r => r.AddAsync(It.IsAny<PlatformUsageLimits>(), It.IsAny<CancellationToken>()))
            .Callback<PlatformUsageLimits, CancellationToken>((entity, _) => added = entity)
            .Returns(Task.CompletedTask);

        var service = new UsageLimitService(limits.Object, usage.Object, workspaces.Object, unitOfWork.Object);
        var result = await service.GetAsync(CancellationToken.None);

        result.TenantsPerUser.Should().Be(1);
        result.WorkspacesPerUser.Should().Be(3);
        result.DocumentsPerWorkspace.Should().Be(200);
        result.StoragePerUserGb.Should().Be(2);
        result.StoragePerTenantGb.Should().Be(5);
        result.StoragePerWorkspaceGb.Should().Be(2);
        result.ProjectsPerWorkspace.Should().Be(10);
        result.PermanentProjectsPerWorkspace.Should().Be(3);
        result.MaxTemporaryProjectLifetimeHours.Should().Be(24 * 30);
        result.LlmProvidersPerUser.Should().Be(3);
        result.EnableOcr.Should().BeTrue();
        result.EnableRagIndexing.Should().BeTrue();
        result.EnableApi.Should().BeTrue();
        added.Should().NotBeNull();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingLimits()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var workspaces = new Mock<IWorkspaceRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var existing = DefaultLimits();
        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = new UsageLimitService(limits.Object, usage.Object, workspaces.Object, unitOfWork.Object);
        var result = await service.SaveAsync(
            new UsageLimitsSaveRequest(
                2,
                4,
                300,
                6,
                8,
                10,
                12,
                5,
                null,
                7,
                false,
                false,
                false),
            CancellationToken.None);

        result.TenantsPerUser.Should().Be(2);
        result.WorkspacesPerUser.Should().Be(4);
        result.DocumentsPerWorkspace.Should().Be(300);
        result.StoragePerUserGb.Should().Be(6);
        result.StoragePerTenantGb.Should().Be(8);
        result.StoragePerWorkspaceGb.Should().Be(10);
        result.ProjectsPerWorkspace.Should().Be(12);
        result.PermanentProjectsPerWorkspace.Should().Be(5);
        result.MaxTemporaryProjectLifetimeHours.Should().BeNull();
        result.LlmProvidersPerUser.Should().Be(7);
        result.EnableOcr.Should().BeFalse();
        result.EnableRagIndexing.Should().BeFalse();
        result.EnableApi.Should().BeFalse();
        existing.UpdatedAt.Should().NotBeNull();
        limits.Verify(r => r.Update(existing), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureTenantCreationAllowedAsync_WhenLimitReached_Throws()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var service = CreateService(limits, usage);
        var userId = Guid.NewGuid();

        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultLimits());
        usage.Setup(u => u.CountTenantsByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => service.EnsureTenantCreationAllowedAsync(userId, CancellationToken.None);

        await act.Should().ThrowAsync<UsageLimitExceededException>();
    }

    [Fact]
    public async Task EnsureDocumentUploadAllowedAsync_WhenWorkspaceDocumentsLimitReached_Throws()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var service = CreateService(limits, usage);
        var workspaceId = Guid.NewGuid();

        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultLimits());
        usage.Setup(u => u.CountDocumentsByWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200);

        var act = () => service.EnsureDocumentUploadAllowedAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            workspaceId,
            1024,
            CancellationToken.None);

        await act.Should().ThrowAsync<UsageLimitExceededException>();
    }

    [Fact]
    public async Task EnsureDocumentUploadAllowedAsync_WhenStorageLimitReached_Throws()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var service = CreateService(limits, usage);
        var workspaceId = Guid.NewGuid();

        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultLimits());
        usage.Setup(u => u.CountDocumentsByWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        usage.Setup(u => u.GetStorageByWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2L * 1024L * 1024L * 1024L);

        var act = () => service.EnsureDocumentUploadAllowedAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            workspaceId,
            1,
            CancellationToken.None);

        await act.Should().ThrowAsync<UsageLimitExceededException>();
    }

    [Fact]
    public async Task EnsureProjectCreationAllowedAsync_WhenWorkspaceProjectLimitReached_Throws()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var workspaces = new Mock<IWorkspaceRepository>();
        var service = CreateService(limits, usage, workspaces);
        var workspaceId = Guid.NewGuid();

        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultLimits());
        workspaces.Setup(w => w.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Id = workspaceId, TenantId = Guid.NewGuid(), Name = "W" });
        usage.Setup(u => u.CountProjectsByWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var act = () => service.EnsureProjectCreationAllowedAsync(workspaceId, CancellationToken.None);

        await act.Should().ThrowAsync<UsageLimitExceededException>();
    }

    [Fact]
    public async Task EnsureLlmProviderCreationAllowedAsync_WhenProviderLimitReached_Throws()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var service = CreateService(limits, usage);
        var userId = Guid.NewGuid();

        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultLimits());
        usage.Setup(u => u.CountLlmProvidersByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var act = () => service.EnsureLlmProviderCreationAllowedAsync(userId, CancellationToken.None);

        await act.Should().ThrowAsync<UsageLimitExceededException>();
    }

    [Fact]
    public async Task EnsureProjectLifetimeAllowedAsync_WhenPermanentLimitReached_Throws()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var workspaces = new Mock<IWorkspaceRepository>();
        var service = CreateService(limits, usage, workspaces);
        var workspaceId = Guid.NewGuid();

        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultLimits());
        workspaces.Setup(w => w.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Id = workspaceId, TenantId = Guid.NewGuid(), Name = "W" });
        usage.Setup(u => u.CountPermanentProjectsByWorkspaceAsync(workspaceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var act = () => service.EnsureProjectLifetimeAllowedAsync(workspaceId, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<UsageLimitExceededException>();
    }

    [Fact]
    public async Task EnsureProjectLifetimeAllowedAsync_WhenLifetimeExceedsLimit_Throws()
    {
        var limits = new Mock<IPlatformUsageLimitsRepository>();
        var usage = new Mock<IUsageMetricsRepository>();
        var workspaces = new Mock<IWorkspaceRepository>();
        var service = CreateService(limits, usage, workspaces);
        var workspaceId = Guid.NewGuid();

        limits.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultLimits());
        workspaces.Setup(w => w.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Id = workspaceId, TenantId = Guid.NewGuid(), Name = "W" });

        var act = () => service.EnsureProjectLifetimeAllowedAsync(
            workspaceId,
            null,
            ProjectLifetimePreset.Day60,
            CancellationToken.None);

        await act.Should().ThrowAsync<UsageLimitExceededException>();
    }

    private static UsageLimitService CreateService(
        Mock<IPlatformUsageLimitsRepository> limits,
        Mock<IUsageMetricsRepository> usage,
        Mock<IWorkspaceRepository>? workspaces = null)
    {
        return new UsageLimitService(
            limits.Object,
            usage.Object,
            (workspaces ?? new Mock<IWorkspaceRepository>()).Object,
            new Mock<IUnitOfWork>().Object);
    }

    private static PlatformUsageLimits DefaultLimits() => new()
    {
        TenantsPerUser = 1,
        WorkspacesPerUser = 3,
        DocumentsPerWorkspace = 200,
        StoragePerUserGb = 2,
        StoragePerTenantGb = 5,
        StoragePerWorkspaceGb = 2,
        ProjectsPerWorkspace = 10,
        PermanentProjectsPerWorkspace = 3,
        MaxTemporaryProjectLifetimeHours = 24 * 30,
        LlmProvidersPerUser = 3,
        EnableOcr = true,
        EnableRagIndexing = true,
        EnableApi = true
    };
}
