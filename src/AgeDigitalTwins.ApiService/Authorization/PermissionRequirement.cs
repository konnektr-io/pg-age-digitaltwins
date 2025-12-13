using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using Microsoft.AspNetCore.Authorization;

namespace AgeDigitalTwins.ApiService.Authorization;

/// <summary>
/// Authorization requirement that specifies a required permission.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the required permission.
    /// </summary>
    public Permission RequiredPermission { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRequirement"/> class.
    /// </summary>
    /// <param name="requiredPermission">The required permission.</param>
    public PermissionRequirement(Permission requiredPermission)
    {
        RequiredPermission = requiredPermission;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRequirement"/> class
    /// from resource and action components.
    /// </summary>
    /// <param name="resource">The resource type.</param>
    /// <param name="action">The permission action.</param>
    public PermissionRequirement(ResourceType resource, PermissionAction action)
        : this(new Permission(resource, action)) { }
}
