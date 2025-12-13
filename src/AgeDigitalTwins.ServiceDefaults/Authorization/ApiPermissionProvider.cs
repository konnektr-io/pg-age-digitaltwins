using System.Security.Claims;
using System.Text.Json;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using AgeDigitalTwins.ServiceDefaults.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ServiceDefaults.Authorization;

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
    
    // Token cache for client credentials
    private class TokenCacheEntry
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
    }

    private TokenCacheEntry? _tokenCache;
    private readonly object _tokenLock = new();

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Use cached token if valid
        if (_tokenCache != null && _tokenCache.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _tokenCache.AccessToken;
        }

        var api = _options.ApiProvider;
        if (api == null || string.IsNullOrEmpty(api.TokenEndpoint) || string.IsNullOrEmpty(api.ClientId) || string.IsNullOrEmpty(api.ClientSecret) || string.IsNullOrEmpty(api.Audience))
        {
            throw new InvalidOperationException("API provider client credentials configuration is missing.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, api.TokenEndpoint);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", api.ClientId),
            new KeyValuePair<string, string>("client_secret", api.ClientSecret),
            new KeyValuePair<string, string>("audience", api.Audience)
        });

        // Use the injected _httpClient for token requests as well
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        if (string.IsNullOrEmpty(accessToken))
            throw new InvalidOperationException("No access_token in client credentials response.");

        var entry = new TokenCacheEntry
        {
            AccessToken = accessToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30) // buffer
        };
        lock (_tokenLock)
        {
            _tokenCache = entry;
        }
        return accessToken;
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
        if (_cache.TryGetValue<IReadOnlyCollection<Permission>>(cacheKey, out var cachedPermissions))
        {
            _logger.LogDebug(
                "Retrieved {Count} permissions from cache for user {UserId}",
                cachedPermissions?.Count ?? 0,
                userId
            );
            return cachedPermissions ?? Array.Empty<Permission>();
        }

        _logger.LogDebug("Calling check permissions API for user {UserId}", userId);

        // Call API to get permissions
        try
        {
            var api = _options.ApiProvider;
            var resourceName = api?.ResourceName ?? "digitaltwins";
            var endpoint = $"{api?.CheckEndpoint ?? "/api/v1/permissions/check"}?scopeType=resource&scopeId={resourceName}&userId={Uri.EscapeDataString(userId ?? "")}";

            _logger.LogDebug(
                "Fetching permissions for user {UserId} from API: {Endpoint}",
                userId,
                endpoint
            );

            // Get M2M token
            var token = await GetAccessTokenAsync(cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();


            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var permissionsProp = doc.RootElement.GetProperty("permissions");
            var permissionStrings = permissionsProp.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .ToArray();

            var permissions = PermissionParser.ParseMany(permissionStrings).ToList().AsReadOnly();

            // Cache the result
            var cacheExpiration = TimeSpan.FromMinutes(
                api?.CacheExpirationMinutes ?? 5
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
