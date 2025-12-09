namespace AgeDigitalTwins.ApiService.Authorization.Models;

/// <summary>
/// Represents a permission with resource and action components.
/// Format: "resource/action" (e.g., "digitaltwins/read").
/// </summary>
public sealed class Permission : IEquatable<Permission>
{
    /// <summary>
    /// Gets the resource type for this permission.
    /// </summary>
    public ResourceType Resource { get; }

    /// <summary>
    /// Gets the action for this permission.
    /// </summary>
    public PermissionAction Action { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Permission"/> class.
    /// </summary>
    /// <param name="resource">The resource type.</param>
    /// <param name="action">The permission action.</param>
    public Permission(ResourceType resource, PermissionAction action)
    {
        Resource = resource;
        Action = action;
    }

    /// <summary>
    /// Gets the string representation of this permission in "resource/action" format.
    /// </summary>
    public override string ToString()
    {
        string resourceStr = Resource switch
        {
            ResourceType.Query => "query",
            ResourceType.DigitalTwins => "digitaltwins",
            ResourceType.Relationships => "digitaltwins/relationships",
            ResourceType.Models => "models",
            ResourceType.JobsImports => "jobs/imports",
            ResourceType.JobsDeletions => "jobs/deletions",
            _ => throw new ArgumentOutOfRangeException(nameof(Resource)),
        };

        string actionStr = Action switch
        {
            PermissionAction.Read => "read",
            PermissionAction.Write => "write",
            PermissionAction.Delete => "delete",
            PermissionAction.Action => "action",
            PermissionAction.Wildcard => "*",
            _ => throw new ArgumentOutOfRangeException(nameof(Action)),
        };

        return $"{resourceStr}/{actionStr}";
    }

    /// <summary>
    /// Checks if this permission grants access for the specified permission.
    /// Handles wildcard matching (e.g., "digitaltwins/*" grants "digitaltwins/read").
    /// </summary>
    /// <param name="required">The required permission.</param>
    /// <returns>True if this permission grants the required access.</returns>
    public bool Grants(Permission required)
    {
        if (Resource != required.Resource)
        {
            return false;
        }

        // Wildcard grants all actions on the resource
        if (Action == PermissionAction.Wildcard)
        {
            return true;
        }

        // Exact match required
        return Action == required.Action;
    }

    public bool Equals(Permission? other)
    {
        if (other is null)
        {
            return false;
        }

        return Resource == other.Resource && Action == other.Action;
    }

    public override bool Equals(object? obj) => Equals(obj as Permission);

    public override int GetHashCode() => HashCode.Combine(Resource, Action);

    public static bool operator ==(Permission? left, Permission? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Permission? left, Permission? right) => !(left == right);
}
