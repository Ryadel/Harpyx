using Harpyx.Application.Extensions;
using Harpyx.Infrastructure.Extensions;
using Harpyx.Worker.Services;

namespace Harpyx.Worker.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddHarpyxWorkerServices(this HostApplicationBuilder builder)
    {
        builder.Services.AddHarpyxInfrastructure(builder.Configuration);
        builder.Services.AddHarpyxApplication();

        builder.Services.AddHostedService<JobConsumerService>();
        builder.Services.AddHostedService<ExpiredProjectsCleanupService>();

        builder.Services.AddHarpyxOpenTelemetry(
            builder.Configuration,
            serviceName: "Harpyx.Worker",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

        return builder;
    }
}
