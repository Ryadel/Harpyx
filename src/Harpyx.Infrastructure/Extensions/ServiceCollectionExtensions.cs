using Harpyx.Application.Interfaces;
using Harpyx.Infrastructure.Configuration;
using Harpyx.Infrastructure.Data;
using Harpyx.Infrastructure.Repositories;
using Harpyx.Infrastructure.Services;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minio;
using StackExchange.Redis;

namespace Harpyx.Infrastructure.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddHarpyxInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HarpyxDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("Harpyx"),
                sql => sql.MigrationsAssembly(typeof(HarpyxDbContext).Assembly.FullName));
        });

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectPromptRepository, ProjectPromptRepository>();
        services.AddScoped<IProjectChatMessageRepository, ProjectChatMessageRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserApiKeyRepository, UserApiKeyRepository>();
        services.AddScoped<IUserTenantRepository, UserTenantRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<ILlmCatalogRepository, LlmCatalogRepository>();
        services.AddScoped<IPlatformSettingsRepository, PlatformSettingsRepository>();
        services.AddScoped<IPlatformUsageLimitsRepository, PlatformUsageLimitsRepository>();
        services.AddScoped<IUserInvitationRepository, UserInvitationRepository>();
        services.AddScoped<IUsageMetricsRepository, UsageMetricsRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var minioOptions = configuration.GetSection("Minio").Get<StorageOptions>() ?? new StorageOptions();
        services.AddSingleton(minioOptions);
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(minioOptions.Endpoint)
            .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
            .WithSSL(minioOptions.UseSsl)
            .Build());
        services.AddScoped<IStorageService, MinioStorageService>();

        var rabbitOptions = configuration.GetSection("RabbitMQ").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        services.AddSingleton(rabbitOptions);
        services.AddSingleton<IJobQueue, RabbitMqJobQueue>();

        var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>() ?? new RedisOptions();
        services.AddSingleton(redisOptions);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions.ConnectionString));
        services.AddScoped<IIdempotencyService, RedisIdempotencyService>();

        var ragOptions = configuration.GetSection("Rag").Get<RagOptions>() ?? new RagOptions();
        services.AddSingleton(ragOptions);

        var openSearchOptions = configuration.GetSection("OpenSearch").Get<OpenSearchOptions>() ?? new OpenSearchOptions();
        services.AddSingleton(openSearchOptions);

        var uploadSecurityOptions = configuration.GetSection("UploadSecurity").Get<UploadSecurityOptions>() ?? new UploadSecurityOptions();
        services.AddSingleton(uploadSecurityOptions);

        var malwareScanOptions = configuration.GetSection("MalwareScan").Get<MalwareScanOptions>() ?? new MalwareScanOptions();
        services.AddSingleton(malwareScanOptions);

        var urlFetchOptions = configuration.GetSection("UrlFetch").Get<UrlFetchOptions>() ?? new UrlFetchOptions();
        services.AddSingleton(urlFetchOptions);

        var encryptionOptions = new EncryptionOptions
        {
            MasterKey = configuration["Encryption:MasterKey"] ?? string.Empty
        };
        services.AddSingleton(encryptionOptions);
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        var emailOptions = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        services.AddSingleton(emailOptions);
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddHttpClient("UrlFetcher");
        services.AddHttpClient("OpenAI");
        services.AddHttpClient("OpenAICompatible");
        services.AddHttpClient("AzureOpenAI");
        services.AddHttpClient("Bedrock");
        services.AddHttpClient("Claude");
        services.AddHttpClient("Google");
        services.AddHttpClient("OpenAIEmbeddings");
        services.AddHttpClient("GoogleEmbeddings");
        services.AddHttpClient("OpenSearch", client =>
        {
            if (Uri.TryCreate(openSearchOptions.Endpoint, UriKind.Absolute, out var endpoint))
                client.BaseAddress = endpoint;

            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, openSearchOptions.RequestTimeoutSeconds));

            if (!string.IsNullOrWhiteSpace(openSearchOptions.Username))
            {
                var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                    $"{openSearchOptions.Username}:{openSearchOptions.Password ?? string.Empty}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
        }).ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (openSearchOptions.AllowInsecureTls)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        });
        services.AddScoped<ILlmClientFactory, LlmClientFactory>();
        services.AddScoped<ILlmClientResolver, LlmClientResolver>();
        services.AddScoped<IEmbeddingService, LlmEmbeddingService>();
        services.AddScoped<IKeywordExtractionService, KeywordExtractionService>();
        services.AddScoped<IOpenSearchChunkIndexService, OpenSearchChunkIndexService>();
        services.AddScoped<IUploadSecurityPolicyService, UploadSecurityPolicyService>();
        services.AddScoped<IFileMalwareScanner, ClamAvFileMalwareScanner>();
        services.AddScoped<ICliOcrService, CliOcrService>();
        services.AddScoped<ILlmOcrService, LlmOcrService>();
        services.AddScoped<ILlmOcrSmokeTestService, LlmOcrSmokeTestService>();
        services.AddScoped<IUrlFetcher, UrlFetcherService>();
        services.AddScoped<IDocumentTextExtractionService, DocumentTextExtractionService>();
        services.AddScoped<IDocumentContainerExpansionService, DocumentContainerExpansionService>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();
        services.AddScoped<IRagIngestionService, RagIngestionService>();
        services.AddScoped<IRagRetrievalService, RagRetrievalService>();

        return services;
    }
}
