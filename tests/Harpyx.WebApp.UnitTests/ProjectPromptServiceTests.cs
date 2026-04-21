using Harpyx.Application.DTOs;
using Harpyx.Domain.Enums;

namespace Harpyx.WebApp.UnitTests;

public class ProjectPromptServiceTests
{
    [Fact]
    public async Task SavePromptUsageAsync_DuplicateContent_UpdatesTimestampOnly()
    {
        var repository = new Mock<IProjectPromptRepository>();
        var projects = new Mock<IProjectService>();
        var settingsService = new Mock<IPlatformSettingsService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var projectId = Guid.NewGuid();
        projects.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var existing = new ProjectPrompt
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PromptType = ProjectPromptType.System,
            Content = "existing prompt",
            ContentHash = "HASH",
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
            IsFavorite = false
        };

        settingsService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings());
        repository.Setup(r => r.GetExactMatchAsync(
                projectId,
                ProjectPromptType.System,
                It.IsAny<string>(),
                "existing prompt",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository.Setup(r => r.GetByProjectAndTypeAsync(
                projectId,
                ProjectPromptType.System,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        var service = new ProjectPromptService(repository.Object, projects.Object, settingsService.Object, unitOfWork.Object);
        var before = existing.LastUsedAt;

        var result = await service.SavePromptUsageAsync(projectId, ProjectPromptType.System, "  existing prompt  ", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(existing.Id);
        existing.LastUsedAt.Should().BeAfter(before);
        repository.Verify(r => r.AddAsync(It.IsAny<ProjectPrompt>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.Update(existing), Times.Once);
        repository.Verify(r => r.RemoveRange(It.IsAny<IReadOnlyList<ProjectPrompt>>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SavePromptUsageAsync_OverflowHistory_TrimsOnlyNonFavorites()
    {
        var repository = new Mock<IProjectPromptRepository>();
        var projects = new Mock<IProjectService>();
        var settingsService = new Mock<IPlatformSettingsService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var projectId = Guid.NewGuid();
        projects.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        IReadOnlyList<ProjectPrompt>? removed = null;

        var favorite = new ProjectPrompt
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PromptType = ProjectPromptType.User,
            Content = "fav",
            ContentHash = "F",
            IsFavorite = true,
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var recent = new ProjectPrompt
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PromptType = ProjectPromptType.User,
            Content = "recent",
            ContentHash = "R",
            IsFavorite = false,
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
        };
        var middle = new ProjectPrompt
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PromptType = ProjectPromptType.User,
            Content = "middle",
            ContentHash = "M",
            IsFavorite = false,
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-3)
        };
        var old = new ProjectPrompt
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PromptType = ProjectPromptType.User,
            Content = "old",
            ContentHash = "O",
            IsFavorite = false,
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-4)
        };

        settingsService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings(userHistory: 2));
        repository.Setup(r => r.GetExactMatchAsync(
                projectId,
                ProjectPromptType.User,
                It.IsAny<string>(),
                "new prompt",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectPrompt?)null);
        repository.Setup(r => r.GetByProjectAndTypeAsync(
                projectId,
                ProjectPromptType.User,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([favorite, recent, middle, old]);
        repository.Setup(r => r.RemoveRange(It.IsAny<IReadOnlyList<ProjectPrompt>>()))
            .Callback<IReadOnlyList<ProjectPrompt>>(items => removed = items);

        var service = new ProjectPromptService(repository.Object, projects.Object, settingsService.Object, unitOfWork.Object);
        await service.SavePromptUsageAsync(projectId, ProjectPromptType.User, "new prompt", CancellationToken.None);

        repository.Verify(r => r.AddAsync(It.IsAny<ProjectPrompt>(), It.IsAny<CancellationToken>()), Times.Once);
        removed.Should().NotBeNull();
        var removedList = removed ?? Array.Empty<ProjectPrompt>();
        removedList.Should().HaveCount(1);
        removedList[0].Id.Should().Be(old.Id);
        removedList[0].IsFavorite.Should().BeFalse();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProjectPromptsAsync_UsesIndependentHistoryLimits()
    {
        var repository = new Mock<IProjectPromptRepository>();
        var projects = new Mock<IProjectService>();
        var settingsService = new Mock<IPlatformSettingsService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var projectId = Guid.NewGuid();
        projects.Setup(p => p.TouchLifetimeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var systemPrompts = new[]
        {
            CreatePrompt(projectId, ProjectPromptType.System, "s1", false, 1),
            CreatePrompt(projectId, ProjectPromptType.System, "s2", false, 2),
            CreatePrompt(projectId, ProjectPromptType.System, "s3", false, 3)
        };
        var userPrompts = new[]
        {
            CreatePrompt(projectId, ProjectPromptType.User, "u1", false, 1),
            CreatePrompt(projectId, ProjectPromptType.User, "u2", false, 2),
            CreatePrompt(projectId, ProjectPromptType.User, "u3", false, 3)
        };

        settingsService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings(systemHistory: 1, userHistory: 3));
        repository.Setup(r => r.GetByProjectAndTypeAsync(projectId, ProjectPromptType.System, It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemPrompts);
        repository.Setup(r => r.GetByProjectAndTypeAsync(projectId, ProjectPromptType.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPrompts);

        var service = new ProjectPromptService(repository.Object, projects.Object, settingsService.Object, unitOfWork.Object);
        var systemResult = await service.GetProjectPromptsAsync(projectId, ProjectPromptType.System, CancellationToken.None);
        var userResult = await service.GetProjectPromptsAsync(projectId, ProjectPromptType.User, CancellationToken.None);

        systemResult.History.Should().HaveCount(1);
        userResult.History.Should().HaveCount(3);
    }

    private static ProjectPrompt CreatePrompt(
        Guid projectId,
        ProjectPromptType promptType,
        string content,
        bool isFavorite,
        int minutesAgo)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PromptType = promptType,
            Content = content,
            ContentHash = content,
            IsFavorite = isFavorite,
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo)
        };

    private static PlatformSettingsDto CreateSettings(
        int systemMaxLength = 16000,
        int userMaxLength = 16000,
        int systemHistory = 20,
        int userHistory = 20)
        => new(
            "default system prompt",
            systemMaxLength,
            userMaxLength,
            systemHistory,
            userHistory,
            30,
            true,
            true,
            true,
            25L * 1024L * 1024L,
            3,
            500L * 1024L * 1024L,
            200,
            100L * 1024L * 1024L,
            6,
            10000,
            60,
            24,
            24,
            12,
            true,
            300,
            true,
            true,
            true);
}
