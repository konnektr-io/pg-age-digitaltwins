using System.Security.Claims;
using AgeDigitalTwins.ApiService.Authorization.Models;
using AgeDigitalTwins.ApiService.Configuration;
using Microsoft.Extensions.Options;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Service for extracting and managing user permissions from JWT claims.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly AuthorizationOptions _options;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        IOptions<AuthorizationOptions> options,
        ILogger<PermissionService> logger
    )
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Permission> GetUserPermissions(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<Permission>();
        }

        // Extract permission claims
        var permissionClaims = user
            .Claims.Where(c => c.Type == _options.PermissionsClaimName)
            .Select(c => c.Value)
            .ToList();

        if (permissionClaims.Count == 0)
        {
            _logger.LogDebug(
                "No permissions found in claims for user. Looking for claim type: {ClaimType}",
                _options.PermissionsClaimName
            );
            return Array.Empty<Permission>();
        }

        // Parse permissions
        var permissions = PermissionParser.ParseMany(permissionClaims).ToList();

        _logger.LogDebug(
            "Found {Count} valid permissions for user: {Permissions}",
            permissions.Count,
            string.Join(", ", permissions.Select(p => p.ToString()))
        );

        return permissions.AsReadOnly();
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
