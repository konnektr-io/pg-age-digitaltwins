// This file is kept for backward compatibility but now uses the shared implementation
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;

namespace AgeDigitalTwins.ApiService.Authorization.Models;

// Re-export the shared enum
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
