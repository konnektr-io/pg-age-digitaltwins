using System.Security.Claims;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ServiceDefaults.Authorization;

/// <summary>
/// Permission provider that extracts permissions from JWT claims.
/// </summary>
/// <remarks>
/// This provider reads permissions from the configured claim type (typically "permissions")
/// in the JWT token and parses them into Permission objects.
/// </remarks>
public class ClaimsPermissionProvider : IPermissionProvider
{
    private readonly string _permissionsClaimName;
    private readonly ILogger<ClaimsPermissionProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimsPermissionProvider"/> class.
    /// </summary>
    /// <param name="permissionsClaimName">The name of the claim that contains permissions (default: "permissions").</param>
    /// <param name="logger">Logger instance.</param>
    public ClaimsPermissionProvider(
        string permissionsClaimName,
        ILogger<ClaimsPermissionProvider> logger
    )
    {
        _permissionsClaimName = permissionsClaimName ?? "permissions";
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimsPermissionProvider"/> class with default claim name.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ClaimsPermissionProvider(ILogger<ClaimsPermissionProvider> logger)
        : this("permissions", logger) { }

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
            .Claims.Where(c => c.Type == _permissionsClaimName)
            .Select(c => c.Value)
            .ToList();

        if (permissionClaims.Count == 0)
        {
            _logger.LogDebug(
                "No permissions found in claims for user. Looking for claim type: {ClaimType}",
                _permissionsClaimName
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
