using System.Security.Claims;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using AgeDigitalTwins.ApiService.Configuration;
using Microsoft.Extensions.Options;

namespace AgeDigitalTwins.ApiService.Authorization;

/// <summary>
/// Permission provider that extracts permissions from JWT claims.
/// Wraps ServiceDefaults.Authorization.ClaimsPermissionProvider with configuration.
/// </summary>
public class ClaimsPermissionProvider : IPermissionProvider
{
    private readonly ServiceDefaults.Authorization.ClaimsPermissionProvider _innerProvider;

    public ClaimsPermissionProvider(
        IOptions<AuthorizationOptions> options,
        ILogger<ServiceDefaults.Authorization.ClaimsPermissionProvider> logger
    )
    {
        _innerProvider = new ServiceDefaults.Authorization.ClaimsPermissionProvider(
            options.Value.PermissionsClaimName,
            logger
        );
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default
    )
    {
        return _innerProvider.GetPermissionsAsync(user, cancellationToken);
    }
}

