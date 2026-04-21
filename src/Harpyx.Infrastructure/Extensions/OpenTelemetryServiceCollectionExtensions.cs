using Harpyx.Application.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Harpyx.Infrastructure.Extensions;

public static class OpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddHarpyxOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string serviceVersion,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        var otlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddSource(
                        HarpyxObservability.UsageLimitsActivitySourceName,
                        HarpyxObservability.JobsActivitySourceName,
                        HarpyxObservability.WebhooksActivitySourceName);

                configureTracing?.Invoke(tracing);

                if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = endpoint);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(
                        HarpyxObservability.UsageLimitsMeterName,
                        HarpyxObservability.JobsMeterName,
                        HarpyxObservability.WebhooksMeterName);

                configureMetrics?.Invoke(metrics);

                if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = endpoint);
                }
            });

        return services;
    }
}
