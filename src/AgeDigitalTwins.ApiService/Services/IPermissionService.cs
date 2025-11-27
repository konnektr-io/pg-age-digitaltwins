using System.Security.Claims;
using AgeDigitalTwins.ApiService.Authorization.Models;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Service for extracting and managing user permissions from JWT claims.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Gets the permissions for the current user from their claims.
    /// </summary>
    /// <param name="user">The claims principal representing the user.</param>
    /// <returns>A collection of permissions granted to the user.</returns>
    IReadOnlyCollection<Permission> GetUserPermissions(ClaimsPrincipal user);

    /// <summary>
    /// Checks if the user has the specified permission.
    /// </summary>
    /// <param name="user">The claims principal representing the user.</param>
    /// <param name="requiredPermission">The required permission.</param>
    /// <returns>True if the user has the permission; otherwise, false.</returns>
    bool HasPermission(ClaimsPrincipal user, Permission requiredPermission);

    /// <summary>
    /// Checks if the user has any of the specified permissions.
    /// </summary>
    /// <param name="user">The claims principal representing the user.</param>
    /// <param name="requiredPermissions">The required permissions (user needs at least one).</param>
    /// <returns>True if the user has any of the permissions; otherwise, false.</returns>
    bool HasAnyPermission(ClaimsPrincipal user, params Permission[] requiredPermissions);
}
