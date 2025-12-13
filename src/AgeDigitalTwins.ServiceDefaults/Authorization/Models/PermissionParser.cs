using System.Diagnostics.CodeAnalysis;

namespace AgeDigitalTwins.ServiceDefaults.Authorization.Models;

/// <summary>
/// Parses permission strings in Azure Digital Twins format.
/// </summary>
public static class PermissionParser
{
    /// <summary>
    /// Parses a permission string in the format "resource/action" or "resource/subresource/action".
    /// </summary>
    /// <param name="permissionString">The permission string to parse (e.g., "digitaltwins/read", "mcp/tools").</param>
    /// <param name="permission">The parsed permission if successful.</param>
    /// <returns>True if parsing was successful; otherwise, false.</returns>
    public static bool TryParse(
        string? permissionString,
        [NotNullWhen(true)] out Permission? permission
    )
    {
        permission = null;

        if (string.IsNullOrWhiteSpace(permissionString))
        {
            return false;
        }

        var parts = permissionString.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return false;
        }

        // Parse action (always the last part)
        string actionStr = parts[^1].ToLowerInvariant();
        PermissionAction action = actionStr switch
        {
            "read" => PermissionAction.Read,
            "write" => PermissionAction.Write,
            "delete" => PermissionAction.Delete,
            "action" => PermissionAction.Action,
            "*" => PermissionAction.Wildcard,
            "tools" => PermissionAction.Wildcard, // Special case: "mcp/tools" means all MCP tool actions
            _ => (PermissionAction)(-1), // Invalid
        };

        if ((int)action == -1)
        {
            return false;
        }

        // Parse resource
        // Reconstruct resource path (everything except the last part)
        string resourcePath = string.Join("/", parts[..^1]).ToLowerInvariant();

        ResourceType resource = resourcePath switch
        {
            "query" => ResourceType.Query,
            "digitaltwins" => ResourceType.DigitalTwins,
            "digitaltwins/relationships" => ResourceType.Relationships,
            "digitaltwins/commands" => ResourceType.DigitalTwins, // Commands are twin actions
            "models" => ResourceType.Models,
            "jobs/imports" => ResourceType.JobsImports,
            "jobs/imports/cancel" => ResourceType.JobsImports, // Cancel is a job action
            "mcp" => ResourceType.Mcp, // Special case: MCP tools
            _ => (ResourceType)(-1), // Invalid
        };

        if ((int)resource == -1)
        {
            return false;
        }

        permission = new Permission(resource, action);
        return true;
    }

    /// <summary>
    /// Parses a permission string and throws an exception if parsing fails.
    /// </summary>
    /// <param name="permissionString">The permission string to parse.</param>
    /// <returns>The parsed permission.</returns>
    /// <exception cref="ArgumentException">Thrown when the permission string is invalid.</exception>
    public static Permission Parse(string permissionString)
    {
        if (!TryParse(permissionString, out var permission))
        {
            throw new ArgumentException(
                $"Invalid permission format: '{permissionString}'. Expected format: 'resource/action' (e.g., 'digitaltwins/read', 'mcp/tools').",
                nameof(permissionString)
            );
        }

        return permission;
    }

    /// <summary>
    /// Parses multiple permission strings from a collection.
    /// </summary>
    /// <param name="permissionStrings">The permission strings to parse.</param>
    /// <returns>A collection of successfully parsed permissions.</returns>
    public static IEnumerable<Permission> ParseMany(IEnumerable<string> permissionStrings)
    {
        foreach (var permissionString in permissionStrings)
        {
            if (TryParse(permissionString, out var permission))
            {
                yield return permission;
            }
        }
    }
}
