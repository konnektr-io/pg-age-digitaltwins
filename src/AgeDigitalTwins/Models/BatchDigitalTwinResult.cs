using System.Collections.Generic;
using System.Linq;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Represents the result of a batch digital twin operation.
/// </summary>
public class BatchDigitalTwinResult
{
    /// <summary>
    /// Gets the results for each individual digital twin operation.
    /// </summary>
    public IReadOnlyList<DigitalTwinOperationResult> Results { get; init; } = [];

    /// <summary>
    /// Gets the number of successful operations.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Gets the number of failed operations.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether any operations failed.
    /// </summary>
    public bool HasFailures => FailureCount > 0;

    /// <summary>
    /// Creates a new instance of <see cref="BatchDigitalTwinResult"/>.
    /// </summary>
    /// <param name="results">The results for each individual digital twin operation.</param>
    public BatchDigitalTwinResult(IReadOnlyList<DigitalTwinOperationResult> results)
    {
        Results = results;
        SuccessCount = results.Count(r => r.IsSuccess);
        FailureCount = results.Count(r => !r.IsSuccess);
    }
}
