using System.Security.Claims;
using System.Text.Json;
using AgeDigitalTwins.ApiService.Authorization.Models;
using AgeDigitalTwins.ApiService.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AgeDigitalTwins.ApiService.Authorization;

/// <summary>
/// Permission provider that retrieves permissions from an external API with caching.
/// </summary>
public class ApiPermissionProvider : IPermissionProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly AuthorizationOptions _options;
    private readonly ILogger<ApiPermissionProvider> _logger;

    public ApiPermissionProvider(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<AuthorizationOptions> options,
        ILogger<ApiPermissionProvider> logger
    )
    {
        _httpClient = httpClientFactory.CreateClient("PermissionsApi");
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Permission>> GetPermissionsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default
    )
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<Permission>();
        }

        // Get user identifier from claims (typically 'sub' claim)
        var userId =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.Identity.Name;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unable to determine user identifier for permission lookup");
            return Array.Empty<Permission>();
        }

        // Check cache first
        var cacheKey = $"permissions:{userId}";
        if (
            _cache.TryGetValue<IReadOnlyCollection<Permission>>(cacheKey, out var cachedPermissions)
        )
        {
            _logger.LogDebug(
                "Retrieved {Count} permissions from cache for user {UserId}",
                cachedPermissions?.Count ?? 0,
                userId
            );
            return cachedPermissions ?? Array.Empty<Permission>();
        }

        // Call API to get permissions
        try
        {
            var resourceName = _options.ApiProvider?.ResourceName ?? "digitaltwins";
            var endpoint =
                $"{_options.ApiProvider?.CheckEndpoint ?? "/api/v1/permissions/check"}?scopeType=resource&scopeId={resourceName}";

            _logger.LogDebug(
                "Fetching permissions for user {UserId} from API: {Endpoint}",
                userId,
                endpoint
            );

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var permissionStrings =
                JsonSerializer.Deserialize<string[]>(content) ?? Array.Empty<string>();

            var permissions = PermissionParser.ParseMany(permissionStrings).ToList().AsReadOnly();

            // Cache the result
            var cacheExpiration = TimeSpan.FromMinutes(
                _options.ApiProvider?.CacheExpirationMinutes ?? 5
            );
            _cache.Set(cacheKey, permissions, cacheExpiration);

            _logger.LogInformation(
                "Retrieved {Count} permissions from API for user {UserId}, cached for {Minutes} minutes",
                permissions.Count,
                userId,
                cacheExpiration.TotalMinutes
            );

            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve permissions from API for user {UserId}. Returning empty permissions.",
                userId
            );
            return Array.Empty<Permission>();
        }
    }
}
