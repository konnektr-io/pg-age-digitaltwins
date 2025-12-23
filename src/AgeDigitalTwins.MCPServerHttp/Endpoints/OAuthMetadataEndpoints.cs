using AgeDigitalTwins.MCPServerHttp.Configuration;
using AgeDigitalTwins.ServiceDefaults.Configuration;
using Microsoft.Extensions.Options;

namespace AgeDigitalTwins.MCPServerHttp.Endpoints;

/// <summary>
/// Provides OAuth 2.1 metadata endpoints for MCP server compliance.
/// </summary>
public static class OAuthMetadataEndpoints
{
    /// <summary>
    /// Maps OAuth metadata endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapOAuthMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        // RFC 9728: Protected Resource Metadata
        // This endpoint is required for MCP OAuth 2.1 compliance
        app.MapGet(
                "/.well-known/oauth-protected-resource",
                (
                    HttpContext context,
                    IConfiguration configuration,
                    IOptions<OAuthMetadataOptions> mcpOptions,
                    IOptions<AuthorizationOptions> authzOptions
                ) =>
                {
                    var mcp = mcpOptions.Value;
                    var authz = authzOptions.Value;

                    // Get the authority from Authentication configuration
                    var authority = configuration["Authentication:Issuer"];

                    // Build the resource server URL if not configured
                    var resourceServerUrl = mcp.ResourceServerUrl;
                    if (string.IsNullOrEmpty(resourceServerUrl))
                    {
                        resourceServerUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                    }

                    return Results.Json(
                        new
                        {
                            resource = resourceServerUrl,
                            authorization_servers = new[] { authority },
                            scopes_supported = authz.ScopesSupported,
                            bearer_methods_supported = new[] { "header" },
                            resource_documentation = "https://docs.konnektr.io/docs/graph",
                            resource_signing_alg_values_supported = new[]
                            {
                                "RS256",
                                "RS384",
                                "RS512",
                            },
                        }
                    );
                }
            )
            .AllowAnonymous()
            .Produces(200)
            .WithName("GetProtectedResourceMetadata")
            .WithTags("OAuth")
            .WithDescription(
                "RFC 9728 Protected Resource Metadata endpoint for OAuth 2.1 discovery"
            );

        return app;
    }
}
