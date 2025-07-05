using System;

namespace AgeDigitalTwins.Jobs.Models;

/// <summary>
/// Result information for import jobs.
/// </summary>
public class ImportJobResult
{
    /// <summary>
    /// Gets or sets the job ID.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the overall job status.
    /// </summary>
    public ImportJobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the job start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the job end time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the number of models successfully created.
    /// </summary>
    public int ModelsCreated { get; set; }

    /// <summary>
    /// Gets or sets the number of twins successfully created.
    /// </summary>
    public int TwinsCreated { get; set; }

    /// <summary>
    /// Gets or sets the number of relationships successfully created.
    /// </summary>
    public int RelationshipsCreated { get; set; }

    /// <summary>
    /// Gets or sets the total number of errors encountered.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets any error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Statistics for a section of the import job.
/// </summary>
public class ImportSectionStats
{
    /// <summary>
    /// Gets or sets the total number of items processed.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of items that succeeded.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the number of items that failed.
    /// </summary>
    public int Failed { get; set; }
}

/// <summary>
/// Status of an import job.
/// </summary>
public enum ImportJobStatus
{
    /// <summary>
    /// The job is starting.
    /// </summary>
    Started,

    /// <summary>
    /// The job is running.
    /// </summary>
    Running,

    /// <summary>
    /// The job completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The job failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The job completed with some failures.
    /// </summary>
    PartiallySucceeded,

    /// <summary>
    /// The job was cancelled.
    /// </summary>
    Cancelled,
}
