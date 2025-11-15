using AgeDigitalTwins.ApiService.Authorization.Models;
using Microsoft.AspNetCore.Authorization;

namespace AgeDigitalTwins.ApiService.Authorization;

/// <summary>
/// Extension methods for applying permission-based authorization to endpoints.
/// </summary>
public static class PermissionExtensions
{
    /// <summary>
    /// Requires the specified permission for this endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="resource">The resource type.</param>
    /// <param name="action">The permission action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TBuilder RequirePermission<TBuilder>(
        this TBuilder builder,
        ResourceType resource,
        PermissionAction action
    )
        where TBuilder : IEndpointConventionBuilder
    {
        var permission = new Permission(resource, action);
        var policyName = $"Permission:{permission}";

        return builder.RequireAuthorization(policyName);
    }

    /// <summary>
    /// Requires the specified permission string for this endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="permissionString">The permission string (e.g., "digitaltwins/read").</param>
    /// <returns>The builder for chaining.</returns>
    public static TBuilder RequirePermission<TBuilder>(
        this TBuilder builder,
        string permissionString
    )
        where TBuilder : IEndpointConventionBuilder
    {
        var permission = PermissionParser.Parse(permissionString);
        var policyName = $"Permission:{permission}";

        return builder.RequireAuthorization(policyName);
    }

    /// <summary>
    /// Adds permission-based authorization policies to the authorization options.
    /// </summary>
    /// <param name="options">The authorization options.</param>
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        // Add a policy for each resource/action combination
        foreach (ResourceType resource in Enum.GetValues<ResourceType>())
        {
            foreach (PermissionAction action in Enum.GetValues<PermissionAction>())
            {
                var permission = new Permission(resource, action);
                var policyName = $"Permission:{permission}";

                options.AddPolicy(
                    policyName,
                    policy => policy.Requirements.Add(new PermissionRequirement(permission))
                );
            }
        }
    }
}
