namespace AgeDigitalTwins.ServiceDefaults.Authorization.Models;

/// <summary>
/// Defines the permission actions aligned with Azure Digital Twins.
/// </summary>
public enum PermissionAction
{
    /// <summary>
    /// Read access (GET operations).
    /// </summary>
    Read,

    /// <summary>
    /// Write access (POST, PUT, PATCH operations).
    /// </summary>
    Write,

    /// <summary>
    /// Delete access (DELETE operations).
    /// </summary>
    Delete,

    /// <summary>
    /// Action access (special operations like cancel).
    /// </summary>
    Action,

    /// <summary>
    /// Wildcard - full access to all operations on the resource.
    /// </summary>
    Wildcard,
}
