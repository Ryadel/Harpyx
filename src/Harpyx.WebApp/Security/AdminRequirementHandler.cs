using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Security;

public class AdminRequirementHandler : AuthorizationHandler<AdminRequirement>
{
    private readonly IUserService _users;

    public AdminRequirementHandler(IUserService users)
    {
        _users = users;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return;

        var role = await _users.ResolveRoleAsync(
            principal.GetObjectId(),
            principal.GetSubjectId(),
            principal.GetEmail(),
            CancellationToken.None);

        if (role == UserRole.Admin)
        {
            context.Succeed(requirement);
        }
    }
}
