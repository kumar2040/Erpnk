using Microsoft.AspNetCore.Authorization;

namespace NkplmErp.Security.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User is null)
        {
            return Task.CompletedTask;
        }

        var permissions = context.User.Claims
            .Where(x => x.Type == "Permission" && x.Value == requirement.Permission);

        if (permissions.Any())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
