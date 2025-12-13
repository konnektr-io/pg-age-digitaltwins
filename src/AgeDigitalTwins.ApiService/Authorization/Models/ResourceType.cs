// This file is kept for backward compatibility but now uses the shared implementation
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;

namespace AgeDigitalTwins.ApiService.Authorization.Models;

// Re-export the shared enum
public enum ResourceType
{
    /// <summary>
    /// Query operations on the digital twins graph.
    /// </summary>
    Query,

    /// <summary>
    /// Digital twin operations (CRUD on twins and components).
    /// </summary>
    DigitalTwins,

    /// <summary>
    /// Relationship operations between digital twins.
    /// </summary>
    Relationships,

    /// <summary>
    /// Model operations (DTDL model management).
    /// </summary>
    Models,

    /// <summary>
    /// Import job operations.
    /// </summary>
    JobsImports,

    /// <summary>
    /// Deletion job operations.
    /// </summary>
    JobsDeletions,
}
