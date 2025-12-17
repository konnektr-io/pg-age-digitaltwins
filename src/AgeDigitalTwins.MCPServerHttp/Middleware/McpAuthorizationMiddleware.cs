using System.Security.Claims;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using AgeDigitalTwins.MCPServerHttp.Configuration;
using Microsoft.Extensions.Options;
using AgeDigitalTwins.ServiceDefaults.Configuration;

namespace AgeDigitalTwins.MCPServerHttp.Middleware;

/// <summary>
/// Middleware for enforcing MCP OAuth scope requirements (RFC 9728).
/// Validates that requests have required OAuth scopes (e.g., "mcp:tools").
/// Permission checking is handled separately by the policy-based authorization system.
/// </summary>
public class McpAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuthorizationOptions _options;
    private readonly ILogger<McpAuthorizationMiddleware> _logger;

    public McpAuthorizationMiddleware(
        RequestDelegate next,
        IOptions<AuthorizationOptions> options,
        ILogger<McpAuthorizationMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for metadata endpoints
        if (context.Request.Path.StartsWithSegments("/.well-known"))
        {
            await _next(context);
            return;
        }

        // Skip if authorization is disabled
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // Check authentication
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await SendUnauthorizedResponseAsync(context);
            return;
        }

        // Check required OAuth scopes (MCP-specific requirement from RFC 9728)
        // Note: Permission checking is handled by the policy-based authorization system,
        // not in this middleware. This middleware only validates OAuth scopes.
        var scopes = context.User
            .FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasRequiredScope = _options.RequiredScopes.Length == 0 || _options.RequiredScopes.Any(required =>
            scopes.Contains(required, StringComparer.OrdinalIgnoreCase));

        if (!hasRequiredScope)
        {
            _logger.LogWarning(
                "Missing required scope. Required: {Required}, Found: {Found}. User: {User}",
                string.Join(", ", _options.RequiredScopes),
                string.Join(", ", scopes),
                context.User.FindFirst("sub")?.Value ?? "Unknown"
            );

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "insufficient_scope",
                error_description = $"Required scopes: {string.Join(", ", _options.RequiredScopes)}",
                scope = string.Join(" ", _options.RequiredScopes)
            });
            return;
        }

        _logger.LogDebug(
            "User {User} authenticated with scopes: {Scopes}",
            context.User.FindFirst("sub")?.Value ?? "Unknown",
            string.Join(", ", scopes)
        );

        await _next(context);
    }

    private async Task SendUnauthorizedResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        
        // Build the resource metadata URL
        var resourceMetadataUrl = $"{context.Request.Scheme}://{context.Request.Host}/.well-known/oauth-protected-resource";
        
        context.Response.Headers.Append("WWW-Authenticate",
            $"Bearer realm=\"mcp\", resource_metadata=\"{resourceMetadataUrl}\"");

        await context.Response.WriteAsJsonAsync(new
        {
            error = "unauthorized",
            error_description = "Authentication required. Please provide a valid Bearer token.",
            resource_metadata = resourceMetadataUrl
        });
    }
}

