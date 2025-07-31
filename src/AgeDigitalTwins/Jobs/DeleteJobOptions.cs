using System;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Options for delete job operations.
/// </summary>
public class DeleteJobOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to continue processing on failure.
    /// Default is false for delete jobs since we want to stop on errors to avoid data inconsistency.
    /// </summary>
    public bool ContinueOnFailure { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of items to process per batch.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the number of items to process before saving a checkpoint.
    /// </summary>
    public int CheckpointInterval { get; set; } = 50;

    /// <summary>
    /// Gets or sets the operation timeout.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(30);
}
