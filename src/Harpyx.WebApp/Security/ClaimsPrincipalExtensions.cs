using System.Security.Claims;

namespace Harpyx.WebApp.Security;

public static class ClaimsPrincipalExtensions
{
    public static string GetEmail(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirst("preferred_username")?.Value
               ?? principal?.FindFirst("email")?.Value
               ?? principal?.FindFirst(ClaimTypes.Email)?.Value
               ?? string.Empty;
    }

    public static string GetSubjectId(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirst("sub")?.Value
               ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? string.Empty;
    }
}