using System.Security.Claims;
using Harpyx.Domain.Enums;

namespace Harpyx.WebApp.Security;

public static class ApiKeyClaimsExtensions
{
    private const string ApiKeyIdClaimType = "harpyx_api_key_id";
    private const string ApiPermissionsClaimType = "harpyx_api_permissions";

    public static bool IsApiKeyIdentity(this ClaimsPrincipal principal)
    {
        return principal?.Identity?.IsAuthenticated == true &&
               principal.HasClaim(claim => claim.Type == ApiKeyIdClaimType);
    }

    public static bool HasApiPermission(this ClaimsPrincipal principal, ApiPermission permission)
    {
        if (!principal.IsApiKeyIdentity())
            return true;

        var raw = principal.FindFirst(ApiPermissionsClaimType)?.Value;
        if (!int.TryParse(raw, out var mask))
            return false;

        var permissions = (ApiPermission)mask;
        return permissions.HasFlag(permission);
    }
}
