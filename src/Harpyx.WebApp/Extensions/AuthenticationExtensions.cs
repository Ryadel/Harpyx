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
                    context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                        ? ApiKeyAuthenticationDefaults.AuthenticationScheme
                        : OpenIdConnectDefaults.AuthenticationScheme;
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
            options.Events ??= new CookieAuthenticationEvents();
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
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
            options.Events.OnTokenValidated = async context =>
            {
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
}
