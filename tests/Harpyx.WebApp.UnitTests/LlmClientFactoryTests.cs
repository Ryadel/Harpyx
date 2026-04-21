using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Harpyx.WebApp.UnitTests;

public class LlmClientFactoryTests
{
    [Fact]
    public async Task Create_OpenAICompatible_UsesBaseUrlAndBearerHeader()
    {
        var handler = new CapturingHandler();
        var factory = BuildFactory(handler);

        var client = factory.Create(LlmProvider.OpenAICompatible, "ollama-key", "http://ollama:11434/v1");

        await client.ChatCompletionAsync("sys", "hi", "llama3", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be("http://ollama:11434/v1/chat/completions");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("ollama-key");
        handler.LastRequest.Headers.Contains("api-key").Should().BeFalse();
    }

    [Fact]
    public async Task Create_AzureOpenAI_UsesApiKeyHeaderNotBearer()
    {
        var handler = new CapturingHandler();
        var factory = BuildFactory(handler);

        var client = factory.Create(
            LlmProvider.AzureOpenAI,
            "azure-key",
            "https://my-resource.openai.azure.com/openai/deployments/gpt-4o");

        await client.ChatCompletionAsync("sys", "hi", "gpt-4o", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://my-resource.openai.azure.com/openai/deployments/gpt-4o/chat/completions");
        handler.LastRequest.Headers.Authorization.Should().BeNull();
        handler.LastRequest.Headers.GetValues("api-key").Single().Should().Be("azure-key");
    }

    [Fact]
    public async Task Create_AmazonBedrock_UsesBaseUrlAndBearerHeader()
    {
        var handler = new CapturingHandler();
        var factory = BuildFactory(handler);

        var client = factory.Create(
            LlmProvider.AmazonBedrock,
            "bedrock-key",
            "https://bedrock-runtime.us-east-1.amazonaws.com/openai/v1");

        await client.ChatCompletionAsync("sys", "hi", "anthropic.claude-3-5-sonnet", CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://bedrock-runtime.us-east-1.amazonaws.com/openai/v1/chat/completions");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("bedrock-key");
    }

    [Fact]
    public void Create_AzureOpenAI_WithoutBaseUrl_Throws()
    {
        var handler = new CapturingHandler();
        var factory = BuildFactory(handler);

        var act = () => factory.Create(LlmProvider.AzureOpenAI, "azure-key", baseUrl: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AzureOpenAI*base URL*");
    }

    [Fact]
    public void Create_AmazonBedrock_WithoutBaseUrl_Throws()
    {
        var handler = new CapturingHandler();
        var factory = BuildFactory(handler);

        var act = () => factory.Create(LlmProvider.AmazonBedrock, "bedrock-key", baseUrl: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AmazonBedrock*base URL*");
    }

    [Fact]
    public async Task Create_OpenAICompatible_WithoutBaseUrl_FallsBackToDefault()
    {
        var handler = new CapturingHandler();
        var factory = BuildFactory(handler);

        var client = factory.Create(LlmProvider.OpenAICompatible, "key", baseUrl: null);

        await client.ChatCompletionAsync("sys", "hi", "gpt-4o", CancellationToken.None);

        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://api.openai.com/v1/chat/completions");
    }

    [Fact]
    public async Task Create_FromLlmConnection_RoutesOpenAICompatibleToStoredBaseUrl()
    {
        var handler = new CapturingHandler();
        var factory = BuildFactory(handler);
        var connection = new LlmConnection
        {
            Scope = LlmConnectionScope.Hosted,
            Provider = LlmProvider.OpenAICompatible,
            BaseUrl = "http://lmstudio:1234/v1"
        };

        var client = factory.Create(connection, "lmstudio-key");

        await client.ChatCompletionAsync("sys", "hi", "local-model", CancellationToken.None);

        handler.LastRequest!.RequestUri!.ToString().Should().Be("http://lmstudio:1234/v1/chat/completions");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("lmstudio-key");
    }

    [Fact]
    public async Task Create_EmptyApiKey_OmitsAuthorizationHeader()
    {
        var handler = new CapturingHandler();
        var factory = BuildFactory(handler);

        var client = factory.Create(LlmProvider.OpenAICompatible, string.Empty, "http://ollama:11434/v1");

        await client.ChatCompletionAsync("sys", "hi", "llama3", CancellationToken.None);

        handler.LastRequest!.Headers.Authorization.Should().BeNull();
        handler.LastRequest.Headers.Contains("api-key").Should().BeFalse();
    }

    private static LlmClientFactory BuildFactory(HttpMessageHandler handler)
    {
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        return new LlmClientFactory(clientFactory.Object, NullLoggerFactory.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}\n\ndata: [DONE]\n\n",
                    System.Text.Encoding.UTF8,
                    "text/event-stream")
            };
            return Task.FromResult(response);
        }
    }
}
