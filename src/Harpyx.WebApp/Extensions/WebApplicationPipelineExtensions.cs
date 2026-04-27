using Harpyx.WebApp.Middleware;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Harpyx.WebApp.Extensions;

public static class WebApplicationPipelineExtensions
{
    public static WebApplication UseHarpyxWebAppPipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler();
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseHarpyxSecurityHeaders();

        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseHarpyxUserAccessGuard();
        app.UseAuthorization();
        app.UseMiddleware<WebhookRequestLoggingMiddleware>();

        app.MapRazorPages();
        app.MapControllers().RequireRateLimiting("api");
        app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
            .RequireRateLimiting("auth");

        app.UseHarpyxSwaggerAccessGuard();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Harpyx API V1");
        });

        app.UseHarpyxApiProblemStatusCodePages();
        app.MapHarpyxHealthChecks();

        return app;
    }

    private static void UseHarpyxSecurityHeaders(this WebApplication app)
    {
        var csp = BuildContentSecurityPolicy(app.Environment.IsDevelopment());

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Frame-Options"] = "DENY";
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                headers["Content-Security-Policy"] = csp;
                return Task.CompletedTask;
            });

            await next();
        });
    }

    private static string BuildContentSecurityPolicy(bool isDevelopment)
    {
        // External auth providers can trigger cross-origin redirects as part of the login flow.
        var formAction = isDevelopment
            ? "form-action 'self' http://localhost:* https://localhost:* https://accounts.google.com https://login.microsoftonline.com"
            : "form-action 'self' https://accounts.google.com https://login.microsoftonline.com";

        var connectSrc = isDevelopment
            ? "connect-src 'self' http://localhost:* https://localhost:* ws://localhost:* wss://localhost:* https://api.iconify.design https://api.unisvg.com https://api.simplesvg.com"
            : "connect-src 'self' https://api.iconify.design https://api.unisvg.com https://api.simplesvg.com";

        return "default-src 'self'; " +
               "base-uri 'self'; " +
               "frame-ancestors 'none'; " +
               formAction + "; " +
               "img-src 'self' data:; " +
               "style-src 'self' 'unsafe-inline'; " +
               "script-src 'self' https://code.iconify.design https://cdn.jsdelivr.net 'unsafe-inline'; " +
               "font-src 'self' data:; " +
               connectSrc;
    }

    private static void UseHarpyxUserAccessGuard(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/AccessDenied")
                || context.Request.Path.StartsWithSegments("/signin-oidc")
                || context.Request.Path.StartsWithSegments("/signin-google")
                || context.Request.Path.StartsWithSegments("/signout-callback-oidc"))
            {
                await next();
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var objectId = context.User.GetObjectId();
                var subjectId = context.User.GetSubjectId();
                var email = context.User.GetEmail();
                var userService = context.RequestServices.GetRequiredService<IUserService>();
                var auditService = context.RequestServices.GetRequiredService<IAuditService>();
                var userIdBefore = await userService.ResolveUserIdAsync(objectId, subjectId, email, context.RequestAborted);
                var authorized = await userService.IsAuthorizedAsync(objectId, subjectId, email, string.Empty, context.RequestAborted);
                if (!authorized)
                {
                    var principalId = !string.IsNullOrWhiteSpace(objectId) ? objectId : subjectId;
                    await auditService.RecordAsync("access_denied", principalId, email, "User could not be provisioned or is inactive", context.RequestAborted);
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    context.Response.Redirect("/AccessDenied");
                    return;
                }

                if (userIdBefore is null)
                {
                    context.Response.Cookies.Append(
                        OnboardingConstants.WelcomeCookieName,
                        "1",
                        new CookieOptions
                        {
                            HttpOnly = true,
                            IsEssential = true,
                            SameSite = SameSiteMode.Lax,
                            Secure = context.Request.IsHttps,
                            MaxAge = TimeSpan.FromMinutes(30)
                        });
                }

                var showWelcome = context.Request.Cookies.TryGetValue(OnboardingConstants.WelcomeCookieName, out var cookieValue) &&
                                  string.Equals(cookieValue, "1", StringComparison.Ordinal);
                var isWelcomeRequest = context.Request.Path.StartsWithSegments("/Welcome");
                var isApiRequest = IsRestApiRequest(context.Request) || context.Request.Path.StartsWithSegments("/swagger");
                if (showWelcome && !isWelcomeRequest && !isApiRequest && HttpMethods.IsGet(context.Request.Method))
                {
                    context.Response.Redirect("/Welcome");
                    return;
                }
            }

            await next();
        });
    }

    private static void UseHarpyxSwaggerAccessGuard(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/swagger") && context.User.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync();
                return;
            }

            await next();
        });
    }

    private static void UseHarpyxApiProblemStatusCodePages(this WebApplication app)
    {
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var context = statusCodeContext.HttpContext;
            if (!IsRestApiRequest(context.Request))
            {
                return;
            }

            if (context.Response.StatusCode < 400 || context.Response.HasStarted)
            {
                return;
            }

            var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = context.Response.StatusCode,
                    Title = context.Features.Get<IExceptionHandlerFeature>()?.Error is null
                        ? "Request failed"
                        : "Unexpected error"
                }
            });
        });
    }

    private static void MapHarpyxHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
                error = entry.Value.Exception?.Message
            }),
            totalDurationMs = report.TotalDuration.TotalMilliseconds
        };

        return context.Response.WriteAsJsonAsync(payload);
    }

    private static bool IsRestApiRequest(HttpRequest request)
    {
        var path = request.Path.Value;
        return string.Equals(path, "/api", StringComparison.Ordinal) ||
               path?.StartsWith("/api/", StringComparison.Ordinal) == true;
    }
}
