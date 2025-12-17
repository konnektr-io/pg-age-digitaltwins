namespace AgeDigitalTwins.ServiceDefaults.Configuration;

/// <summary>
/// Configuration options for authorization.
/// </summary>
public class AuthorizationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether authorization is enabled.
    /// When false, all authorization checks are bypassed (for development/testing only).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the permission provider type.
    /// Supported values: "Claims" (default), "Api".
    /// </summary>
    public string Provider { get; set; } = "Claims";

    /// <summary>
    /// Gets or sets the name of the claim that contains user permissions.
    /// Default is "permissions". Only used when Provider is "Claims".
    /// </summary>
    public string PermissionsClaimName { get; set; } = "permissions";

    /// <summary>
    /// Gets or sets the required scopes.
    /// Should be ["mcp:tools"] for mcp access.
    /// </summary>
    public string[] RequiredScopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the supported scopes.
    /// Should be ["mcp:tools", "mcp:resources"] for MCP.
    /// </summary>
    public string[] ScopesSupported { get; set; } = [];

    /// <summary>
    /// Gets or sets the API provider configuration.
    /// Only used when Provider is "Api".
    /// </summary>
    public ApiProviderOptions? ApiProvider { get; set; }
}

/// <summary>
/// Configuration options for the API-based permission provider.
/// </summary>
public class ApiProviderOptions
{
    /// <summary>
    /// Gets or sets the base URL of the permissions API.
    /// Example: "https://ktrlplane.konnektr.io"
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the endpoint path for checking permissions.
    /// Default is "/api/v1/permissions/check".
    /// </summary>
    public string CheckEndpoint { get; set; } = "/api/v1/permissions/check";

    /// <summary>
    /// Gets or sets the resource name to check permissions for.
    /// Example: "digitaltwins", "graph-instance-123".
    /// </summary>
    public string ResourceName { get; set; } = "digitaltwins";

    /// <summary>
    /// Gets or sets the cache expiration time in minutes.
    /// Default is 5 minutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the API timeout in seconds.
    /// Default is 10 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the OAuth2 token endpoint for client credentials flow.
    /// Example: "https://YOUR_DOMAIN/oauth/token"
    /// </summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the audience for the M2M token.
    /// Example: "https://api.ktrlplane.konnektr.io"
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID for client credentials auth.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret for client credentials auth.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}
