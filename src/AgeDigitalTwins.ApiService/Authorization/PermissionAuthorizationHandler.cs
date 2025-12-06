using AgeDigitalTwins.ApiService.Services;
using Microsoft.AspNetCore.Authorization;

namespace AgeDigitalTwins.ApiService.Authorization;

/// <summary>
/// Authorization handler for permission-based authorization.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger
    )
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement
    )
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("User is not authenticated");
            return Task.CompletedTask;
        }

        // Check if user has the required permission
        if (_permissionService.HasPermission(context.User, requirement.RequiredPermission))
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

        return Task.CompletedTask;
    }
}
