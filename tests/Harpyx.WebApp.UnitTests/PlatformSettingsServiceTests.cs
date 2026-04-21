using Harpyx.Application.DTOs;
using Harpyx.Application.Defaults;

namespace Harpyx.WebApp.UnitTests;

public class PlatformSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_WhenSettingsMissing_CreatesDefault()
    {
        var repository = new Mock<IPlatformSettingsRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        PlatformSettings? added = null;

        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlatformSettings?)null);
        repository.Setup(r => r.AddAsync(It.IsAny<PlatformSettings>(), It.IsAny<CancellationToken>()))
            .Callback<PlatformSettings, CancellationToken>((settings, _) => added = settings)
            .Returns(Task.CompletedTask);

        var service = new PlatformSettingsService(repository.Object, unitOfWork.Object);
        var result = await service.GetAsync(CancellationToken.None);

        result.DefaultSystemPrompt.Should().Be(PromptDefaults.DefaultSystemPrompt);
        result.SystemPromptMaxLengthChars.Should().Be(PromptDefaults.SystemPromptMaxLengthChars);
        result.UserPromptMaxLengthChars.Should().Be(PromptDefaults.UserPromptMaxLengthChars);
        result.SystemPromptHistoryLimitPerProject.Should().Be(PromptDefaults.SystemPromptHistoryLimitPerProject);
        result.UserPromptHistoryLimitPerProject.Should().Be(PromptDefaults.UserPromptHistoryLimitPerProject);
        result.UserSelfRegistrationEnabled.Should().BeTrue();
        result.QuarantineEnabled.Should().BeTrue();
        result.UrlDocumentsEnabled.Should().BeTrue();
        result.MaxFileSizeBytes.Should().Be(25L * 1024L * 1024L);
        result.ContainerMaxNestingDepth.Should().Be(3);
        result.ContainerMaxTotalExtractedBytesPerRoot.Should().Be(500L * 1024L * 1024L);
        result.ContainerMaxFilesPerRoot.Should().Be(200);
        result.ContainerMaxSingleEntrySizeBytes.Should().Be(100L * 1024L * 1024L);
        result.RagTopK.Should().Be(6);
        result.RagUseRakeKeywordExtraction.Should().BeTrue();
        result.RagUseOpenSearchIndexing.Should().BeTrue();
        result.RagUseOpenSearchRetrieval.Should().BeTrue();
        result.RagFallbackToSqlRetrievalOnOpenSearchFailure.Should().BeTrue();
        added.Should().NotBeNull();
        added!.DefaultSystemPrompt.Should().Be(PromptDefaults.DefaultSystemPrompt);
        added.SystemPromptMaxLengthChars.Should().Be(PromptDefaults.SystemPromptMaxLengthChars);
        added.UserPromptMaxLengthChars.Should().Be(PromptDefaults.UserPromptMaxLengthChars);
        added.SystemPromptHistoryLimitPerProject.Should().Be(PromptDefaults.SystemPromptHistoryLimitPerProject);
        added.UserPromptHistoryLimitPerProject.Should().Be(PromptDefaults.UserPromptHistoryLimitPerProject);
        added.Should().NotBeNull();
        added.QuarantineEnabled.Should().BeTrue();
        added.MaxFileSizeBytes.Should().Be(25L * 1024L * 1024L);
        added.ContainerMaxNestingDepth.Should().Be(3);
        added.ContainerMaxTotalExtractedBytesPerRoot.Should().Be(500L * 1024L * 1024L);
        added.ContainerMaxFilesPerRoot.Should().Be(200);
        added.ContainerMaxSingleEntrySizeBytes.Should().Be(100L * 1024L * 1024L);
        added.RagTopK.Should().Be(6);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingSettings()
    {
        var repository = new Mock<IPlatformSettingsRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var existing = new PlatformSettings
        {
            Id = Guid.NewGuid(),
            DefaultSystemPrompt = "old prompt",
            SystemPromptMaxLengthChars = 1000,
            UserPromptMaxLengthChars = 1200,
            SystemPromptHistoryLimitPerProject = 10,
            UserPromptHistoryLimitPerProject = 12,
            UserSelfRegistrationEnabled = true,
            QuarantineEnabled = true,
            MaxFileSizeBytes = 25L * 1024L * 1024L,
            ContainerMaxNestingDepth = 3,
            ContainerMaxTotalExtractedBytesPerRoot = 500L * 1024L * 1024L,
            ContainerMaxFilesPerRoot = 200,
            ContainerMaxSingleEntrySizeBytes = 100L * 1024L * 1024L,
            RagTopK = 6,
            RagMaxContextChars = 10000,
            RagRrfK = 60,
            RagLexicalCandidateK = 24,
            RagVectorCandidateK = 24,
            RagKeywordMaxCount = 12,
            RagUseRakeKeywordExtraction = true,
            RagContextCacheTtlSeconds = 300,
            RagUseOpenSearchIndexing = false,
            RagUseOpenSearchRetrieval = false,
            RagFallbackToSqlRetrievalOnOpenSearchFailure = true
        };

        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = new PlatformSettingsService(repository.Object, unitOfWork.Object);
        var result = await service.SaveAsync(
            new PlatformSettingsSaveRequest(
                "new system prompt",
                12000,
                14000,
                32,
                28,
                30,
                false,
                false,
                true,
                64L * 1024L * 1024L,
                4,
                700L * 1024L * 1024L,
                350,
                120L * 1024L * 1024L,
                8,
                22000,
                70,
                30,
                28,
                16,
                false,
                600,
                true,
                true,
                false),
            CancellationToken.None);

        result.DefaultSystemPrompt.Should().Be("new system prompt");
        result.SystemPromptMaxLengthChars.Should().Be(12000);
        result.UserPromptMaxLengthChars.Should().Be(14000);
        result.SystemPromptHistoryLimitPerProject.Should().Be(32);
        result.UserPromptHistoryLimitPerProject.Should().Be(28);
        result.UserSelfRegistrationEnabled.Should().BeFalse();
        result.QuarantineEnabled.Should().BeFalse();
        result.UrlDocumentsEnabled.Should().BeTrue();
        result.MaxFileSizeBytes.Should().Be(64L * 1024L * 1024L);
        result.ContainerMaxNestingDepth.Should().Be(4);
        result.ContainerMaxTotalExtractedBytesPerRoot.Should().Be(700L * 1024L * 1024L);
        result.ContainerMaxFilesPerRoot.Should().Be(350);
        result.ContainerMaxSingleEntrySizeBytes.Should().Be(120L * 1024L * 1024L);
        result.RagTopK.Should().Be(8);
        result.RagUseRakeKeywordExtraction.Should().BeFalse();
        result.RagUseOpenSearchIndexing.Should().BeTrue();
        result.RagUseOpenSearchRetrieval.Should().BeTrue();
        result.RagFallbackToSqlRetrievalOnOpenSearchFailure.Should().BeFalse();
        repository.Verify(
            r => r.Update(
                It.Is<PlatformSettings>(s =>
                    s.Id == existing.Id &&
                    s.DefaultSystemPrompt == "new system prompt" &&
                    s.SystemPromptMaxLengthChars == 12000 &&
                    s.UserPromptMaxLengthChars == 14000 &&
                    s.SystemPromptHistoryLimitPerProject == 32 &&
                    s.UserPromptHistoryLimitPerProject == 28 &&
                    !s.UserSelfRegistrationEnabled &&
                    !s.QuarantineEnabled &&
                    s.MaxFileSizeBytes == 64L * 1024L * 1024L &&
                    s.ContainerMaxNestingDepth == 4 &&
                    s.ContainerMaxTotalExtractedBytesPerRoot == 700L * 1024L * 1024L &&
                    s.ContainerMaxFilesPerRoot == 350 &&
                    s.ContainerMaxSingleEntrySizeBytes == 120L * 1024L * 1024L &&
                    s.RagTopK == 8 &&
                    !s.RagUseRakeKeywordExtraction &&
                    s.RagUseOpenSearchIndexing &&
                    s.RagUseOpenSearchRetrieval &&
                    !s.RagFallbackToSqlRetrievalOnOpenSearchFailure)),
            Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
