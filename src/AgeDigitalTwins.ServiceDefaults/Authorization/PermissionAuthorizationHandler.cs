using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ServiceDefaults.Authorization;

/// <summary>
/// Authorization handler for permission-based authorization.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionProvider _permissionProvider;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionProvider permissionProvider,
        ILogger<PermissionAuthorizationHandler> logger
    )
    {
        _permissionProvider = permissionProvider;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement
    )
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("User is not authenticated");
            return;
        }

        // Check if user has the required permission
        var permissions = await _permissionProvider.GetPermissionsAsync(context.User);
        var hasPermission = permissions.Any(p => p.Grants(requirement.RequiredPermission));

        if (hasPermission)
        {
            _logger.LogDebug(
                "User has required permission: {Permission}",
                requirement.RequiredPermission
            );
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "User lacks required permission: {Permission}. User: {User}",
                requirement.RequiredPermission,
                context.User.Identity.Name ?? "Unknown"
            );
        }
    }
}
