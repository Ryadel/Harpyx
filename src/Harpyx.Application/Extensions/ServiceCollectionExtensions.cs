using Harpyx.Application.Interfaces;
using Harpyx.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Harpyx.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHarpyxApplication(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectPromptService, ProjectPromptService>();
        services.AddScoped<IProjectChatMessageService, ProjectChatMessageService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITenantMembershipService, TenantMembershipService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ILlmConnectionSmokeTestService, LlmConnectionSmokeTestService>();
        services.AddScoped<IUserLlmProviderService, UserLlmProviderService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<IUserInvitationService, UserInvitationService>();
        services.AddScoped<IUsageLimitService, UsageLimitService>();

        return services;
    }
}
