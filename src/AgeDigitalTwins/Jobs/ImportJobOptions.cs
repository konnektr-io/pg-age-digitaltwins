namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Options for import job operations.
/// </summary>
public class ImportJobOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to continue processing on failure.
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
}
