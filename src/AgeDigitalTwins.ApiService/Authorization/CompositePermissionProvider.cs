using System.Security.Claims;
using AgeDigitalTwins.ApiService.Authorization.Models;

namespace AgeDigitalTwins.ApiService.Authorization;

public class CompositePermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<IPermissionProvider> _providers;
    private readonly ILogger<CompositePermissionProvider> _logger;

    public CompositePermissionProvider(
        IEnumerable<IPermissionProvider> providers,
        ILogger<CompositePermissionProvider> logger
    )
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default
    )
    {
        var allPermissions = new HashSet<Permission>();

        foreach (var provider in _providers)
        {
            try
            {
                var permissions = await provider.GetPermissionsAsync(user, cancellationToken);
                foreach (var permission in permissions)
                {
                    allPermissions.Add(permission);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving permissions from provider {ProviderType}",
                    provider.GetType().Name
                );
            }
        }

        _logger.LogDebug(
            "Aggregated {Count} unique permissions for user {User}",
            allPermissions.Count,
            user.Identity?.Name ?? "Unknown"
        );

        return allPermissions.ToList().AsReadOnly();
    }
}
