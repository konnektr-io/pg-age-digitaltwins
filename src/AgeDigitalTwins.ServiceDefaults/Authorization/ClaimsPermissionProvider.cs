using System.Security.Claims;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using AgeDigitalTwins.ServiceDefaults.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgeDigitalTwins.ServiceDefaults.Authorization;

/// <summary>
/// Permission provider that extracts permissions from JWT claims.
/// </summary>
public class ClaimsPermissionProvider : IPermissionProvider
{
    private readonly AuthorizationOptions _options;
    private readonly ILogger<ClaimsPermissionProvider> _logger;

    public ClaimsPermissionProvider(
        IOptions<AuthorizationOptions> options,
        ILogger<ClaimsPermissionProvider> logger
    )
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default
    )
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult<IReadOnlyCollection<Permission>>(Array.Empty<Permission>());
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
            return Task.FromResult<IReadOnlyCollection<Permission>>(Array.Empty<Permission>());
        }

        // Parse permissions and remove duplicates
        var permissions = PermissionParser.ParseMany(permissionClaims).Distinct().ToList();

        _logger.LogDebug(
            "Found {Count} valid permissions for user from claims: {Permissions}",
            permissions.Count,
            string.Join(", ", permissions.Select(p => p.ToString()))
        );

        return Task.FromResult<IReadOnlyCollection<Permission>>(permissions.AsReadOnly());
    }
}
