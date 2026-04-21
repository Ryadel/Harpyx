using System.Linq;

namespace Harpyx.WebApp.UnitTests;

public class LlmClientResolverTests
{
    private readonly Mock<ILlmCatalogRepository> _catalog = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly Mock<ILlmClientFactory> _clientFactory = new();
    private readonly Mock<ILlmClient> _client = new();

    private LlmClientResolver CreateResolver()
    {
        _clientFactory
            .Setup(f => f.Create(It.IsAny<LlmConnection>(), It.IsAny<string>()))
            .Returns(_client.Object);

        return new LlmClientResolver(
            _catalog.Object,
            _encryption.Object,
            _clientFactory.Object);
    }

    [Fact]
    public async Task ResolveAsync_NoModel_ReturnsNotConfigured()
    {
        var userId = Guid.NewGuid();
        _catalog.Setup(r => r.GetPreferenceAsync(userId, LlmProviderType.Chat, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLlmModelPreference?)null);
        _catalog.Setup(r => r.GetSelectableModelsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(userId)),
                LlmProviderType.Chat,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LlmModel>());

        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync(userId, null, null, CancellationToken.None);

        result.IsConfigured.Should().BeFalse();
        result.Client.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_DefaultChatModel_ReturnsConfiguredClient()
    {
        var userId = Guid.NewGuid();
        var model = BuildPersonalModel(userId, LlmProviderType.Chat, "gpt-4o");
        _catalog.Setup(r => r.GetPreferenceAsync(userId, LlmProviderType.Chat, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserLlmModelPreference
            {
                UserId = userId,
                Usage = LlmProviderType.Chat,
                LlmModelId = model.Id,
                LlmModel = model
            });
        _encryption.Setup(e => e.Decrypt("encrypted-key")).Returns("sk-test-key");

        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync(userId, null, null, CancellationToken.None);

        result.IsConfigured.Should().BeTrue();
        result.Client.Should().BeSameAs(_client.Object);
        result.Model.Should().Be("gpt-4o");
        _clientFactory.Verify(f => f.Create(model.Connection, "sk-test-key"), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ModelWithoutChatCapability_ReturnsNotConfigured()
    {
        var userId = Guid.NewGuid();
        var model = BuildPersonalModel(userId, LlmProviderType.RagEmbedding, "text-embedding-3-small");
        _catalog.Setup(r => r.GetModelByIdAsync(model.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);

        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync(userId, model.Id, null, CancellationToken.None);

        result.IsConfigured.Should().BeFalse();
        result.Client.Should().BeNull();
    }

    private static LlmModel BuildPersonalModel(Guid userId, LlmProviderType capability, string modelId)
    {
        var connection = new LlmConnection
        {
            UserId = userId,
            Scope = LlmConnectionScope.Personal,
            Provider = LlmProvider.OpenAI,
            EncryptedApiKey = "encrypted-key",
            IsEnabled = true
        };

        var model = new LlmModel
        {
            Connection = connection,
            ConnectionId = connection.Id,
            Capability = capability,
            ModelId = modelId,
            IsEnabled = true,
            IsPublished = true
        };
        connection.Models.Add(model);
        return model;
    }
}
