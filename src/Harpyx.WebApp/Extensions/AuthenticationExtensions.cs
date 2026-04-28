using Harpyx.Application.Interfaces;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Harpyx.WebApp.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddHarpyxAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = HarpyxAuthenticationDefaults.SmartScheme;
                options.DefaultScheme = HarpyxAuthenticationDefaults.SmartScheme;
                options.DefaultChallengeScheme = HarpyxAuthenticationDefaults.ChallengeScheme;
                options.DefaultForbidScheme = HarpyxAuthenticationDefaults.ChallengeScheme;
            })
            .AddPolicyScheme(HarpyxAuthenticationDefaults.SmartScheme, HarpyxAuthenticationDefaults.SmartScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                    HasApiKeyAuthorizationHeader(context.Request)
                        ? ApiKeyAuthenticationDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme(HarpyxAuthenticationDefaults.ChallengeScheme, HarpyxAuthenticationDefaults.ChallengeScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                    IsRestApiRequest(context.Request)
                        ? ApiKeyAuthenticationDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                _ => { })
            .AddGoogle("Google", options =>
            {
                options.ClientId = configuration["Authentication:Google:ClientId"] ?? string.Empty;
                options.ClientSecret = configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
                options.CallbackPath = configuration["Authentication:Google:CallbackPath"] ?? "/signin-google";

                options.Events ??= new OAuthEvents();
                options.Events.OnCreatingTicket = async context =>
                {
                    var objectId = context.Principal?.GetObjectId() ?? string.Empty;
                    var subjectId = context.Principal?.GetSubjectId() ?? string.Empty;
                    var email = context.Principal.GetEmail();
                    var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    var userIdBefore = await userService.ResolveUserIdAsync(objectId, subjectId, email, context.HttpContext.RequestAborted);
                    await userService.IsAuthorizedAsync(objectId, subjectId, email, context.Scheme.Name, context.HttpContext.RequestAborted);
                    if (userIdBefore is null)
                    {
                        context.HttpContext.Response.Cookies.Append(
                            OnboardingConstants.WelcomeCookieName,
                            "1",
                            new CookieOptions
                            {
                                HttpOnly = true,
                                IsEssential = true,
                                SameSite = SameSiteMode.Lax,
                                Secure = context.HttpContext.Request.IsHttps,
                                MaxAge = TimeSpan.FromMinutes(30)
                            });
                    }
                };
            }).AddMicrosoftIdentityWebApp(configuration.GetSection("Authentication:EntraId"));

        services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/AccessDenied";
            options.Events ??= new CookieAuthenticationEvents();
            options.Events.OnRedirectToLogin = context =>
            {
                if (IsRestApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (IsRestApiRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.SaveTokens = true;

            options.Events ??= new OpenIdConnectEvents();
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                GetEntraLogger(context.HttpContext).LogInformation(
                    "Redirecting to Microsoft Entra ID. RedirectUri: {RedirectUri}; ResponseType: {ResponseType}; Scope: {Scope}; Request: {Scheme}://{Host}{PathBase}{Path}",
                    context.ProtocolMessage.RedirectUri,
                    context.ProtocolMessage.ResponseType,
                    context.ProtocolMessage.Scope,
                    context.Request.Scheme,
                    context.Request.Host.Value,
                    context.Request.PathBase.Value,
                    context.Request.Path.Value);
                return Task.CompletedTask;
            };
            options.Events.OnMessageReceived = context =>
            {
                var logger = GetEntraLogger(context.HttpContext);

                if (!string.IsNullOrWhiteSpace(context.ProtocolMessage.Error))
                {
                    logger.LogWarning(
                        "Microsoft Entra ID callback returned an error. Error: {Error}; Description: {ErrorDescription}; ErrorUri: {ErrorUri}; HasCode: {HasCode}; HasState: {HasState}; Method: {Method}; Path: {Path}",
                        context.ProtocolMessage.Error,
                        Truncate(context.ProtocolMessage.ErrorDescription),
                        context.ProtocolMessage.ErrorUri,
                        !string.IsNullOrWhiteSpace(context.ProtocolMessage.Code),
                        !string.IsNullOrWhiteSpace(context.ProtocolMessage.State),
                        context.Request.Method,
                        context.Request.Path.Value);
                }
                else
                {
                    logger.LogInformation(
                        "Microsoft Entra ID callback received. HasCode: {HasCode}; HasState: {HasState}; Method: {Method}; Path: {Path}",
                        !string.IsNullOrWhiteSpace(context.ProtocolMessage.Code),
                        !string.IsNullOrWhiteSpace(context.ProtocolMessage.State),
                        context.Request.Method,
                        context.Request.Path.Value);
                }

                return Task.CompletedTask;
            };
            options.Events.OnTokenValidated = async context =>
            {
                GetEntraLogger(context.HttpContext).LogInformation(
                    "Microsoft Entra ID token validated. HasOid: {HasOid}; HasSub: {HasSub}; HasEmail: {HasEmail}; HasName: {HasName}",
                    !string.IsNullOrWhiteSpace(context.Principal?.GetObjectId()),
                    !string.IsNullOrWhiteSpace(context.Principal?.GetSubjectId()),
                    !string.IsNullOrWhiteSpace(context.Principal.GetEmail()),
                    !string.IsNullOrWhiteSpace(context.Principal?.Identity?.Name));

                var objectId = context.Principal?.GetObjectId() ?? string.Empty;
                var subjectId = context.Principal?.GetSubjectId() ?? string.Empty;
                var email = context.Principal.GetEmail();
                var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                var provider = context.Scheme.Name;
                var userIdBefore = await userService.ResolveUserIdAsync(objectId, subjectId, email, context.HttpContext.RequestAborted);
                await userService.IsAuthorizedAsync(objectId, subjectId, email, provider, context.HttpContext.RequestAborted);
                if (userIdBefore is null)
                {
                    context.HttpContext.Response.Cookies.Append(
                        OnboardingConstants.WelcomeCookieName,
                        "1",
                        new CookieOptions
                        {
                            HttpOnly = true,
                            IsEssential = true,
                            SameSite = SameSiteMode.Lax,
                            Secure = context.HttpContext.Request.IsHttps,
                            MaxAge = TimeSpan.FromMinutes(30)
                        });
                }
                var auditService = context.HttpContext.RequestServices.GetRequiredService<IAuditService>();
                var principalId = !string.IsNullOrWhiteSpace(objectId) ? objectId : subjectId;
                await auditService.RecordAsync("login", principalId, email, provider + " login", context.HttpContext.RequestAborted);
            };
            options.Events.OnAuthenticationFailed = context =>
            {
                GetEntraLogger(context.HttpContext).LogError(
                    context.Exception,
                    "Microsoft Entra ID callback authentication failed. Method: {Method}; Path: {Path}; Message: {Message}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    Truncate(context.Exception.Message));
                context.Response.Redirect("/Account/Login?error=entra");
                context.HandleResponse();
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                GetEntraLogger(context.HttpContext).LogError(
                    context.Failure,
                    "Microsoft Entra ID remote authentication failed. Method: {Method}; Path: {Path}; Failure: {Failure}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    Truncate(context.Failure?.Message));
                context.Response.Redirect("/Account/Login?error=entra");
                context.HandleResponse();
                return Task.CompletedTask;
            };
        });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = options.DefaultPolicy;
            options.AddPolicy("AdminOnly", policy =>
                policy.Requirements.Add(new AdminRequirement()));
        });
        services.AddScoped<IAuthorizationHandler, AdminRequirementHandler>();

        services.AddAntiforgery();

        return services;
    }

    private static bool HasApiKeyAuthorizationHeader(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var authorizationValues))
            return false;

        var authorization = authorizationValues.ToString();
        return authorization.StartsWith(
            ApiKeyAuthenticationDefaults.AuthorizationHeaderScheme + " ",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRestApiRequest(HttpRequest request)
    {
        var path = request.Path.Value;
        return string.Equals(path, "/api", StringComparison.Ordinal) ||
               path?.StartsWith("/api/", StringComparison.Ordinal) == true;
    }

    private static Microsoft.Extensions.Logging.ILogger GetEntraLogger(HttpContext httpContext) =>
        httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Harpyx.EntraId");

    private static string? Truncate(string? value, int maxLength = 700)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
