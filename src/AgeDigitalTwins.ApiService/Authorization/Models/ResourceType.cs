namespace AgeDigitalTwins.ApiService.Authorization.Models;

/// <summary>
/// Defines the resource types for authorization aligned with Azure Digital Twins permissions.
/// </summary>
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
    /// Graph operations (Create/Drop graph).
    /// </summary>
    Graph,
}
