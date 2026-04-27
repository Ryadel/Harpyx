using System.Net;
using System.Threading.RateLimiting;
using Harpyx.WebApp.HealthChecks;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Harpyx.WebApp.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddHarpyxWebAppServices(this WebApplicationBuilder builder)
    {
        ConfigureForwardedHeaders(builder.Services, builder.Configuration);
        ConfigureDataProtection(builder);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.AuthorizeFolder("/");
            options.Conventions.AuthorizeFolder("/Tenants", "AdminOnly");
            options.Conventions.AuthorizeFolder("/Users", "AdminOnly");
            options.Conventions.AuthorizeAreaFolder("Admin", "/", "AdminOnly");
            options.Conventions.AllowAnonymousToPage("/Index");
            options.Conventions.AllowAnonymousToPage("/AccessDenied");
            options.Conventions.AllowAnonymousToFolder("/Account");
            options.Conventions.AllowAnonymousToFolder("/Help");
        })
            .AddViewLocalization()
            .AddDataAnnotationsLocalization();

        builder.Services.AddControllers(options =>
        {
            // Protect all mutating controller actions behind antiforgery checks.
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        });

        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });

        ConfigureRateLimiting(builder.Services);
        ConfigureHealthChecks(builder.Services);
        builder.Services.AddHarpyxOpenTelemetry(
            builder.Configuration,
            serviceName: "Harpyx.WebApp",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            configureTracing: tracing => tracing.AddAspNetCoreInstrumentation(),
            configureMetrics: metrics => metrics.AddAspNetCoreInstrumentation());
        ConfigureSwagger(builder.Services, builder.Configuration);

        builder.Services.AddHarpyxInfrastructure(builder.Configuration);
        builder.Services.AddHarpyxApplication();
        builder.Services.AddHarpyxAuthentication(builder.Configuration);

        builder.Services.AddScoped<ITenantScopeService, TenantScopeService>();

        return builder;
    }

    private static void ConfigureForwardedHeaders(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = Math.Max(1, configuration.GetValue<int?>("ReverseProxy:ForwardLimit") ?? 1);

            var knownProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>();
            foreach (var proxy in knownProxies)
            {
                if (IPAddress.TryParse(proxy, out var ipAddress))
                {
                    options.KnownProxies.Add(ipAddress);
                }
            }

            var knownNetworks = configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>();
            foreach (var network in knownNetworks)
            {
                var parsedNetwork = ParseNetwork(network);
                if (parsedNetwork is System.Net.IPNetwork value)
                {
                    options.KnownIPNetworks.Add(value);
                }
            }
        });
    }

    private static void ConfigureDataProtection(WebApplicationBuilder builder)
    {
        var dataProtectionBuilder = builder.Services.AddDataProtection()
            .SetApplicationName(builder.Configuration["DataProtection:ApplicationName"] ?? "Harpyx");

        if (!builder.Environment.IsProduction())
        {
            return;
        }

        var configuredKeysPath = builder.Configuration["DataProtection:KeysPath"];
        var keysPath = string.IsNullOrWhiteSpace(configuredKeysPath)
            ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")
            : ResolvePath(builder.Environment.ContentRootPath, configuredKeysPath);

        Directory.CreateDirectory(keysPath);
        dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }

    private static void ConfigureRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (rateLimitContext, _) =>
            {
                if (rateLimitContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    rateLimitContext.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
                }

                var problemDetailsService = rateLimitContext.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = rateLimitContext.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Title = "Too many requests",
                        Detail = "Rate limit exceeded. Try again later.",
                        Status = StatusCodes.Status429TooManyRequests
                    }
                });
            };

            options.AddPolicy("api", httpContext =>
            {
                var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(key, static _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("auth", httpContext =>
            {
                var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(key, static _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 12,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
        });
    }

    private static void ConfigureHealthChecks(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Alive."), tags: ["live"])
            .AddCheck<SqlServerReadinessHealthCheck>("sql", tags: ["ready"])
            .AddCheck<RedisReadinessHealthCheck>("redis", tags: ["ready"])
            .AddCheck<OpenSearchReadinessHealthCheck>("opensearch", tags: ["ready"])
            .AddCheck<RabbitMqReadinessHealthCheck>("rabbitmq", tags: ["ready"])
            .AddCheck<MinioReadinessHealthCheck>("minio", tags: ["ready"])
            .AddCheck<ClamAvReadinessHealthCheck>("clamav", tags: ["ready"]);

        services.AddSingleton<IHealthCheckPublisher, HealthAlertPublisher>();
        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Predicate = registration => registration.Tags.Contains("ready");
            options.Delay = TimeSpan.FromSeconds(15);
            options.Period = TimeSpan.FromSeconds(60);
        });
    }

    private static void ConfigureSwagger(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "Harpyx API", Version = "v1" });
            options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
                Name = "Authorization",
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Description = "Format: Authorization: ApiKey {your-api-key}"
            });
            options.AddSecurityDefinition("oauth2", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.SecuritySchemeType.OpenIdConnect,
                OpenIdConnectUrl = new Uri(configuration["Authentication:EntraId:Authority"] + "/v2.0/.well-known/openid-configuration")
            });
            options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.OpenApiSecuritySchemeReference("ApiKey", document, null),
                    new List<string>()
                }
            });
            options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.OpenApiSecuritySchemeReference("oauth2", document, null),
                    new List<string>()
                }
            });
        });
    }

    private static string ResolvePath(string contentRootPath, string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }

    private static System.Net.IPNetwork? ParseNetwork(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        if (!IPAddress.TryParse(parts[0], out var address) || !int.TryParse(parts[1], out var prefixLength))
        {
            return null;
        }

        var maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            return null;
        }

        return new System.Net.IPNetwork(address, prefixLength);
    }
}
