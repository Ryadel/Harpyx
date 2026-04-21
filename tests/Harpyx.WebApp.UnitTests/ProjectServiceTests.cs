using FluentAssertions;
using Harpyx.Application.Interfaces;
using Harpyx.Application.Services;
using Moq;
using Xunit;

namespace Harpyx.WebApp.UnitTests;

public class ProjectServiceTests
{
    [Fact]
    public async Task ProjectService_CreatesAndReturnsProject()
    {
        var repo = new Mock<IProjectRepository>();
        var workspaces = new Mock<IWorkspaceRepository>();
        var documents = new Mock<IDocumentRepository>();
        var chunks = new Mock<IDocumentChunkRepository>();
        var catalog = new Mock<ILlmCatalogRepository>();
        var usageLimits = new Mock<IUsageLimitService>();
        var jobQueue = new Mock<IJobQueue>();
        var storage = new Mock<IStorageService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        workspaces
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid workspaceId, CancellationToken _) => new Workspace
            {
                Id = workspaceId,
                TenantId = Guid.NewGuid(),
                Name = "W"
            });
        usageLimits
            .Setup(p => p.EnsureProjectCreationAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        usageLimits
            .Setup(p => p.EnsureProjectLifetimeAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Harpyx.Domain.Enums.ProjectLifetimePreset?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ProjectService(
            repo.Object,
            workspaces.Object,
            documents.Object,
            chunks.Object,
            catalog.Object,
            usageLimits.Object,
            jobQueue.Object,
            storage.Object,
            unitOfWork.Object);

        var workspaceId = Guid.NewGuid();
        var result = await service.CreateAsync(
            new Harpyx.Application.DTOs.CreateProjectRequest(workspaceId, "Project A", "desc"),
            CancellationToken.None
        );

        result.Name.Should().Be("Project A");
        result.WorkspaceId.Should().Be(workspaceId);
        repo.Verify(r => r.AddAsync(It.IsAny<Harpyx.Domain.Entities.Project>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
