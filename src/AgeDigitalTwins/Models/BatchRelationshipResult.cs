using System;
using System.Collections.Generic;
using System.Linq;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Represents the result of a batch relationship operation.
/// </summary>
public class BatchRelationshipResult
{
    /// <summary>
    /// Gets the collection of individual relationship operation results.
    /// </summary>
    public IReadOnlyCollection<RelationshipOperationResult> Results { get; init; } =
        Array.Empty<RelationshipOperationResult>();

    /// <summary>
    /// Gets the total number of successful operations.
    /// </summary>
    public int SuccessCount => Results.Count(r => r.IsSuccess);

    /// <summary>
    /// Gets the total number of failed operations.
    /// </summary>
    public int FailureCount => Results.Count(r => !r.IsSuccess);

    /// <summary>
    /// Gets a value indicating whether any operations failed.
    /// </summary>
    public bool HasFailures => FailureCount > 0;
}
