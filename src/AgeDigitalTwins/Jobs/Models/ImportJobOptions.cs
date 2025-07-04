using System;

namespace AgeDigitalTwins.Jobs.Models;

/// <summary>
/// Configuration options for import jobs.
/// </summary>
public class ImportJobOptions
{
    /// <summary>
    /// Gets or sets the maximum number of items to process in a single batch for models.
    /// Default is 100.
    /// </summary>
    public int ModelBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to continue processing on individual item failures.
    /// Default is true.
    /// </summary>
    public bool ContinueOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for individual operations.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
