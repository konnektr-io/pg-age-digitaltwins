namespace AgeDigitalTwins.ApiService.Configuration;

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
    /// Gets or sets the name of the claim that contains user permissions.
    /// Default is "permissions".
    /// </summary>
    public string PermissionsClaimName { get; set; } = "permissions";

    /// <summary>
    /// Gets or sets a value indicating whether to use strict validation.
    /// When true, requests without valid permissions will be rejected.
    /// When false, requests with missing/invalid permissions will be logged but allowed (for migration).
    /// </summary>
    public bool StrictMode { get; set; } = true;
}
