using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Harpyx.WebApp.Security;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ObjectIdClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string ApiPermissionsClaimType = "harpyx_api_permissions";

    private readonly IApiKeyService _apiKeys;
    private readonly IUsageLimitService _usageLimits;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeys,
        IUsageLimitService usageLimits)
        : base(options, logger, encoder)
    {
        _apiKeys = apiKeys;
        _usageLimits = usageLimits;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationValues))
            return AuthenticateResult.NoResult();

        var authorization = authorizationValues.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var headerValue))
            return AuthenticateResult.NoResult();

        if (!string.Equals(
                headerValue.Scheme,
                ApiKeyAuthenticationDefaults.AuthorizationHeaderScheme,
                StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var rawApiKey = headerValue.Parameter?.Trim();
        if (string.IsNullOrWhiteSpace(rawApiKey))
            return AuthenticateResult.Fail("Missing API key.");

        var validation = await _apiKeys.ValidateAsync(rawApiKey, Context.RequestAborted);
        if (validation is null)
            return AuthenticateResult.Fail("Invalid or expired API key.");
        if (!await _usageLimits.IsApiEnabledForUserAsync(validation.UserId, Context.RequestAborted))
            return AuthenticateResult.Fail("API access disabled for this instance.");

        var claims = new List<Claim>
        {
            new("preferred_username", validation.Email),
            new(ClaimTypes.Email, validation.Email),
            new(ClaimTypes.Name, validation.Email),
            new("harpyx_user_id", validation.UserId.ToString("D")),
            new("harpyx_api_key_id", validation.ApiKeyId.ToString("D")),
            new(ApiPermissionsClaimType, ((int)validation.Permissions).ToString())
        };

        if (!string.IsNullOrWhiteSpace(validation.SubjectId))
            claims.Add(new Claim("sub", validation.SubjectId));

        if (!string.IsNullOrWhiteSpace(validation.ObjectId))
            claims.Add(new Claim(ObjectIdClaimType, validation.ObjectId));

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        Response.Headers.WWWAuthenticate = ApiKeyAuthenticationDefaults.AuthorizationHeaderScheme;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = (int)HttpStatusCode.Forbidden;
        return Task.CompletedTask;
    }
}
