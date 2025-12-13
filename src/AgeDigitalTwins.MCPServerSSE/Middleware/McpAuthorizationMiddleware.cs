using System.Security.Claims;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using AgeDigitalTwins.MCPServerSSE.Configuration;
using Microsoft.Extensions.Options;
using AgeDigitalTwins.ServiceDefaults.Configuration;

namespace AgeDigitalTwins.MCPServerSSE.Middleware;

/// <summary>
/// Middleware for enforcing MCP authorization based on scopes and permissions.
/// Validates that requests have required OAuth scopes and permissions to access MCP tools.
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

    public async Task InvokeAsync(HttpContext context, IPermissionProvider? permissionProvider = null)
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

        // Check required scopes
        var scopes = context.User
            .FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasRequiredScope = _options.RequiredScopes.Any(required =>
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

        // Check for MCP tool permission if permission provider is available
        if (permissionProvider != null)
        {
            var permissions = await permissionProvider.GetPermissionsAsync(context.User);
            var requiredPermission = new Permission(ResourceType.McpTools, PermissionAction.Wildcard);
            
            if (!permissions.Any(p => p.Grants(requiredPermission)))
            {
                _logger.LogWarning(
                    "User {User} lacks required permission: {Permission}",
                    context.User.FindFirst("sub")?.Value ?? "Unknown",
                    requiredPermission
                );

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "insufficient_permissions",
                    error_description = "Required permission: mcp/tools"
                });
                return;
            }
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

