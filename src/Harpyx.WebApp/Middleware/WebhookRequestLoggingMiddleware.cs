using System.Diagnostics;
using Harpyx.Application.Telemetry;

namespace Harpyx.WebApp.Middleware;

public class WebhookRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebhookRequestLoggingMiddleware> _logger;

    public WebhookRequestLoggingMiddleware(RequestDelegate next, ILogger<WebhookRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/webhooks", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var webhookEventId =
            context.Request.Headers["Stripe-Signature"].FirstOrDefault()
            ?? context.Request.Headers["X-Webhook-Event-Id"].FirstOrDefault()
            ?? context.Request.Headers["X-Event-Id"].FirstOrDefault()
            ?? "n/a";

        using var activity = HarpyxObservability.WebhooksActivitySource.StartActivity("Webhooks.Request", ActivityKind.Server);
        activity?.SetTag("http.route", context.Request.Path.ToString());
        activity?.SetTag("webhook.event_id", webhookEventId);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            HarpyxObservability.WebhookRequestsCounter.Add(
                1,
                new("path", context.Request.Path.ToString()),
                new("status_code", statusCode));

            _logger.LogInformation(
                "Webhook request processed. Path={Path}, EventId={EventId}, StatusCode={StatusCode}, DurationMs={DurationMs}",
                context.Request.Path,
                webhookEventId,
                statusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
