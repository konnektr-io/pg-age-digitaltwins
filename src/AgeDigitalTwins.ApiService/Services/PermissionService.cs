using System.Security.Claims;
using AgeDigitalTwins.ApiService.Authorization;
using AgeDigitalTwins.ApiService.Authorization.Models;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Service for extracting and managing user permissions using a pluggable provider.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IPermissionProvider _provider;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(IPermissionProvider provider, ILogger<PermissionService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Permission> GetUserPermissions(ClaimsPrincipal user)
    {
        // Synchronous wrapper for async provider
        return _provider.GetPermissionsAsync(user).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public bool HasPermission(ClaimsPrincipal user, Permission requiredPermission)
    {
        var userPermissions = GetUserPermissions(user);

        // Check if any user permission grants the required permission
        var hasPermission = userPermissions.Any(p => p.Grants(requiredPermission));

        _logger.LogDebug(
            "Permission check for {RequiredPermission}: {Result}",
            requiredPermission,
            hasPermission ? "GRANTED" : "DENIED"
        );

        return hasPermission;
    }

    /// <inheritdoc />
    public bool HasAnyPermission(ClaimsPrincipal user, params Permission[] requiredPermissions)
    {
        if (requiredPermissions.Length == 0)
        {
            return true;
        }

        var userPermissions = GetUserPermissions(user);

        // Check if any user permission grants any of the required permissions
        var hasPermission = requiredPermissions.Any(required =>
            userPermissions.Any(userPerm => userPerm.Grants(required))
        );

        _logger.LogDebug(
            "Permission check for any of [{RequiredPermissions}]: {Result}",
            string.Join(", ", requiredPermissions.Select(p => p.ToString())),
            hasPermission ? "GRANTED" : "DENIED"
        );

        return hasPermission;
    }
}
