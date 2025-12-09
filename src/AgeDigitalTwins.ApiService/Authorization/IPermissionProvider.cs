using System.Security.Claims;
using AgeDigitalTwins.ApiService.Authorization.Models;

namespace AgeDigitalTwins.ApiService.Authorization;

/// <summary>
/// Defines a strategy for retrieving user permissions from different sources.
/// </summary>
public interface IPermissionProvider
{
    /// <summary>
    /// Retrieves permissions for the specified user.
    /// </summary>
    /// <param name="user">The claims principal representing the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of permissions granted to the user.</returns>
    Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default
    );
}
