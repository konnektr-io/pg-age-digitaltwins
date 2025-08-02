using System;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Options for import job operations.
/// </summary>
public class ImportJobOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to continue processing on failure.
    /// </summary>
    public bool ContinueOnFailure { get; set; } = true;

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

    /// <summary>
    /// Gets or sets the heartbeat and cancellation check interval.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to leave the input and output streams open after processing.
    /// When true, the streams will not be disposed automatically.
    /// </summary>
    public bool LeaveOpen { get; set; } = false;
}
