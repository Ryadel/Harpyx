using Harpyx.Application.DTOs;

namespace Harpyx.WebApp.UnitTests;

public class WorkspaceServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesWorkspaceAndPersists()
    {
        var repository = new Mock<IWorkspaceRepository>();
        var catalog = new Mock<ILlmCatalogRepository>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        Workspace? addedWorkspace = null;

        repository.Setup(r => r.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((workspace, _) => addedWorkspace = workspace)
            .Returns(Task.CompletedTask);
        usageLimits.Setup(p => p.EnsureWorkspaceCreationAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new WorkspaceService(repository.Object, catalog.Object, usageLimits.Object, unitOfWork.Object);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await service.CreateAsync(
            new CreateWorkspaceRequest(userId, tenantId, "Finance", "Financial analysis workspace", true),
            CancellationToken.None);

        result.TenantId.Should().Be(tenantId);
        result.Name.Should().Be("Finance");
        result.Description.Should().Be("Financial analysis workspace");
        result.IsActive.Should().BeTrue();
        addedWorkspace.Should().NotBeNull();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkspaceExists_UpdatesAndPersists()
    {
        var repository = new Mock<IWorkspaceRepository>();
        var catalog = new Mock<ILlmCatalogRepository>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Initial",
            Description = "Initial description",
            IsActive = true
        };

        repository.Setup(r => r.GetByIdAsync(workspace.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var service = new WorkspaceService(repository.Object, catalog.Object, usageLimits.Object, unitOfWork.Object);
        var updateRequest = new WorkspaceDto(
            workspace.Id,
            workspace.TenantId,
            "Updated",
            "Updated description",
            false);

        var result = await service.UpdateAsync(updateRequest, CancellationToken.None);

        result.Name.Should().Be("Updated");
        result.Description.Should().Be("Updated description");
        result.IsActive.Should().BeFalse();
        repository.Verify(r => r.Update(It.Is<Workspace>(w =>
            w.Id == workspace.Id &&
            w.Name == "Updated" &&
            w.Description == "Updated description" &&
            w.IsActive == false)), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenWorkspaceDoesNotExist_Throws()
    {
        var repository = new Mock<IWorkspaceRepository>();
        var catalog = new Mock<ILlmCatalogRepository>();
        var usageLimits = new Mock<IUsageLimitService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new WorkspaceService(repository.Object, catalog.Object, usageLimits.Object, unitOfWork.Object);

        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var act = () => service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Workspace not found.");
    }
}
