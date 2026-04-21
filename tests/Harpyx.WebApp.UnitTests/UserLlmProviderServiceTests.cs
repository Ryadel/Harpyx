using System.Linq;
using Harpyx.Application.DTOs;

namespace Harpyx.WebApp.UnitTests;

public class UserLlmProviderServiceTests
{
    private readonly Mock<ILlmCatalogRepository> _catalog = new();
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly Mock<IDocumentChunkRepository> _chunkRepo = new();
    private readonly Mock<IDocumentRepository> _documentRepo = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly Mock<IEmbeddingService> _embedding = new();
    private readonly Mock<ILlmClientFactory> _llmClientFactory = new();
    private readonly Mock<ILlmClient> _llmClient = new();
    private readonly Mock<ILlmOcrSmokeTestService> _ocrSmokeTest = new();
    private readonly Mock<IUsageLimitService> _usageLimits = new();
    private readonly Mock<IJobQueue> _jobQueue = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private UserLlmProviderService CreateService()
        => new(
            _catalog.Object,
            _projectRepo.Object,
            _workspaceRepo.Object,
            _chunkRepo.Object,
            _documentRepo.Object,
            _encryption.Object,
            new LlmConnectionSmokeTestService(
                _embedding.Object,
                _llmClientFactory.Object,
                _ocrSmokeTest.Object),
            _usageLimits.Object,
            _jobQueue.Object,
            _unitOfWork.Object);

    public UserLlmProviderServiceTests()
    {
        _llmClientFactory
            .Setup(f => f.Create(It.IsAny<LlmProvider>(), It.IsAny<string>()))
            .Returns(_llmClient.Object);

        _llmClient
            .Setup(c => c.ChatCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult(true, "ok", null));

        _embedding
            .Setup(e => e.EmbedAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<LlmProvider>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new[] { new float[] { 0.1f, 0.2f } });

        _ocrSmokeTest
            .Setup(s => s.ValidateAsync(It.IsAny<LlmProvider>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _usageLimits
            .Setup(p => p.EnsureLlmProviderCreationAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _projectRepo.Setup(r => r.GetByChatModelIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Project>());
        _projectRepo.Setup(r => r.GetByRagModelIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Project>());
        _projectRepo.Setup(r => r.GetByOcrModelIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Project>());

        _workspaceRepo.Setup(r => r.GetByChatModelIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Workspace>());
        _workspaceRepo.Setup(r => r.GetByRagModelIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Workspace>());
        _workspaceRepo.Setup(r => r.GetByOcrModelIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Workspace>());

        _catalog.Setup(r => r.GetPreferencesByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserLlmModelPreference>());
    }

    [Fact]
    public async Task GetAllAsync_NoProviders_ReturnsEmptyAndNoCapabilitiesConfigured()
    {
        _catalog.Setup(r => r.GetPersonalConnectionsByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LlmConnection>());

        var service = CreateService();
        var result = await service.GetAllAsync(Guid.NewGuid(), CancellationToken.None);

        result.Providers.Should().BeEmpty();
        result.HasAnyChatConfigured.Should().BeFalse();
        result.HasAnyRagConfigured.Should().BeFalse();
        result.HasAnyOcrConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WithoutCapabilities_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = new LlmProviderSaveRequest(
            LlmProvider.OpenAI,
            "sk-test",
            EnableChat: false,
            EnableRagEmbedding: false,
            EnableOcr: false,
            ChatModel: null,
            RagEmbeddingModel: null,
            OcrModel: null,
            SetAsDefaultChat: false,
            SetAsDefaultRagEmbedding: false,
            SetAsDefaultOcr: false);

        var act = () => service.SaveAsync(Guid.NewGuid(), request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one capability*");
    }

    [Fact]
    public async Task SaveAsync_NewProviderWithCapabilities_PersistsModelsAndDefaults()
    {
        var userId = Guid.NewGuid();
        LlmConnection? addedConnection = null;
        var models = new List<LlmModel>();
        var preferences = new List<UserLlmModelPreference>();

        _catalog.Setup(r => r.GetPersonalConnectionByProviderAsync(userId, LlmProvider.OpenAI, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmConnection?)null);
        _catalog.Setup(r => r.AddConnectionAsync(It.IsAny<LlmConnection>(), It.IsAny<CancellationToken>()))
            .Callback<LlmConnection, CancellationToken>((connection, _) => addedConnection = connection)
            .Returns(Task.CompletedTask);
        _catalog.Setup(r => r.GetModelsByConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid connectionId, CancellationToken _) => models.Where(m => m.ConnectionId == connectionId).ToList());
        _catalog.Setup(r => r.AddModelAsync(It.IsAny<LlmModel>(), It.IsAny<CancellationToken>()))
            .Callback<LlmModel, CancellationToken>((model, _) =>
            {
                model.Connection = addedConnection!;
                addedConnection!.Models.Add(model);
                models.Add(model);
            })
            .Returns(Task.CompletedTask);
        _catalog.Setup(r => r.GetModelByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid modelId, CancellationToken _) => models.FirstOrDefault(m => m.Id == modelId));
        _catalog.Setup(r => r.GetPreferenceAsync(userId, It.IsAny<LlmProviderType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, LlmProviderType usage, CancellationToken __) => preferences.FirstOrDefault(p => p.Usage == usage));
        _catalog.Setup(r => r.AddPreferenceAsync(It.IsAny<UserLlmModelPreference>(), It.IsAny<CancellationToken>()))
            .Callback<UserLlmModelPreference, CancellationToken>((preference, _) => preferences.Add(preference))
            .Returns(Task.CompletedTask);
        _catalog.Setup(r => r.GetPreferencesByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => preferences);

        _encryption.Setup(e => e.Encrypt("sk-proj-test1234")).Returns("encrypted-value");

        var service = CreateService();
        var request = new LlmProviderSaveRequest(
            LlmProvider.OpenAI,
            "sk-proj-test1234",
            EnableChat: true,
            EnableRagEmbedding: true,
            EnableOcr: true,
            ChatModel: "gpt-4o",
            RagEmbeddingModel: "text-embedding-3-large",
            OcrModel: "gpt-4o-mini",
            SetAsDefaultChat: true,
            SetAsDefaultRagEmbedding: true,
            SetAsDefaultOcr: true,
            Name: "Primary Assistant",
            Description: "Main provider",
            Notes: "Internal annotation");

        var result = await service.SaveAsync(userId, request, CancellationToken.None);

        result.Provider.Should().Be(LlmProvider.OpenAI);
        result.IsConfigured.Should().BeTrue();
        result.SupportsChat.Should().BeTrue();
        result.SupportsRagEmbedding.Should().BeTrue();
        result.SupportsOcr.Should().BeTrue();
        result.ChatModel.Should().Be("gpt-4o");
        result.RagEmbeddingModel.Should().Be("text-embedding-3-large");
        result.OcrModel.Should().Be("gpt-4o-mini");
        result.IsDefaultChat.Should().BeTrue();
        result.IsDefaultRagEmbedding.Should().BeTrue();
        result.IsDefaultOcr.Should().BeTrue();
        models.Should().HaveCount(3);
        preferences.Should().HaveCount(3);

        _catalog.Verify(r => r.AddConnectionAsync(It.Is<LlmConnection>(p =>
            p.UserId == userId &&
            p.Scope == LlmConnectionScope.Personal &&
            p.Provider == LlmProvider.OpenAI &&
            p.Name == "Primary Assistant" &&
            p.Description == "Main provider" &&
            p.Notes == "Internal annotation" &&
            p.ApiKeyLast4 == "1234"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_AzureOpenAiProvider_PersistsEndpointAndUsesEndpointAwareSmokeTest()
    {
        var userId = Guid.NewGuid();
        LlmConnection? addedConnection = null;
        var models = new List<LlmModel>();

        _catalog.Setup(r => r.GetPersonalConnectionByProviderAsync(userId, LlmProvider.AzureOpenAI, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmConnection?)null);
        _catalog.Setup(r => r.AddConnectionAsync(It.IsAny<LlmConnection>(), It.IsAny<CancellationToken>()))
            .Callback<LlmConnection, CancellationToken>((connection, _) => addedConnection = connection)
            .Returns(Task.CompletedTask);
        _catalog.Setup(r => r.GetModelsByConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid connectionId, CancellationToken _) => models.Where(m => m.ConnectionId == connectionId).ToList());
        _catalog.Setup(r => r.AddModelAsync(It.IsAny<LlmModel>(), It.IsAny<CancellationToken>()))
            .Callback<LlmModel, CancellationToken>((model, _) =>
            {
                model.Connection = addedConnection!;
                addedConnection!.Models.Add(model);
                models.Add(model);
            })
            .Returns(Task.CompletedTask);

        _encryption.Setup(e => e.Encrypt("azure-key-1234")).Returns("encrypted-azure");
        _llmClientFactory
            .Setup(f => f.Create(LlmProvider.AzureOpenAI, "azure-key-1234", "https://example.openai.azure.com/openai/v1"))
            .Returns(_llmClient.Object);

        var service = CreateService();
        var request = new LlmProviderSaveRequest(
            LlmProvider.AzureOpenAI,
            "azure-key-1234",
            EnableChat: true,
            EnableRagEmbedding: true,
            EnableOcr: false,
            ChatModel: "harpyx-chat-deployment",
            RagEmbeddingModel: "harpyx-embedding-deployment",
            OcrModel: null,
            SetAsDefaultChat: false,
            SetAsDefaultRagEmbedding: false,
            SetAsDefaultOcr: false,
            BaseUrl: "https://example.openai.azure.com/openai/v1/");

        var result = await service.SaveAsync(userId, request, CancellationToken.None);

        result.Provider.Should().Be(LlmProvider.AzureOpenAI);
        result.BaseUrl.Should().Be("https://example.openai.azure.com/openai/v1");
        addedConnection!.BaseUrl.Should().Be("https://example.openai.azure.com/openai/v1");
        models.Should().HaveCount(2);

        _embedding.Verify(e => e.EmbedAsync(
            It.IsAny<IReadOnlyList<string>>(),
            LlmProvider.AzureOpenAI,
            "azure-key-1234",
            "harpyx-embedding-deployment",
            It.IsAny<CancellationToken>(),
            "https://example.openai.azure.com/openai/v1"), Times.Once);
        _llmClientFactory.Verify(f => f.Create(
            LlmProvider.AzureOpenAI,
            "azure-key-1234",
            "https://example.openai.azure.com/openai/v1"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_RagModelUsedByProjects_ResetsProjectIndexes()
    {
        var userId = Guid.NewGuid();
        var connection = new LlmConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Scope = LlmConnectionScope.Personal,
            Provider = LlmProvider.OpenAI,
            EncryptedApiKey = "encrypted"
        };
        var ragModel = new LlmModel
        {
            Id = Guid.NewGuid(),
            Connection = connection,
            ConnectionId = connection.Id,
            Capability = LlmProviderType.RagEmbedding,
            ModelId = "text-embedding-3-small"
        };
        connection.Models.Add(ragModel);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "P1",
            WorkspaceId = Guid.NewGuid(),
            RagEmbeddingModelId = ragModel.Id,
            RagIndexVersion = 2
        };

        var document = new Document
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            StorageKey = "obj-1",
            State = DocumentState.Completed
        };

        _catalog.Setup(r => r.GetPersonalConnectionByProviderAsync(userId, LlmProvider.OpenAI, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _projectRepo.Setup(r => r.GetByRagModelIdAsync(ragModel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        _documentRepo.Setup(r => r.GetByProjectAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { document });

        var service = CreateService();
        var result = await service.DeleteAsync(userId, LlmProvider.OpenAI, CancellationToken.None);

        result.Deleted.Should().BeTrue();
        result.AffectedProjectCount.Should().Be(1);
        project.RagEmbeddingModelId.Should().BeNull();
        project.RagIndexVersion.Should().Be(3);
        document.State.Should().Be(DocumentState.Queued);

        _chunkRepo.Verify(r => r.RemoveByProjectIdAsync(project.Id, It.IsAny<CancellationToken>()), Times.Once);
        _jobQueue.Verify(q => q.EnqueueParseJobAsync(document.Id, It.IsAny<CancellationToken>()), Times.Once);
        _catalog.Verify(r => r.RemoveConnection(connection), Times.Once);
    }
}
