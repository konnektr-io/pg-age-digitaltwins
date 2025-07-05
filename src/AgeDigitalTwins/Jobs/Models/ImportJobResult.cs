using System;

namespace AgeDigitalTwins.Jobs.Models;

/// <summary>
/// Result information for import jobs, compatible with Azure Digital Twins API.
/// </summary>
public class ImportJobResult
{
    /// <summary>
    /// Gets or sets the job ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the input Azure storage blob that contains file(s) describing the operations to perform in the job.
    /// </summary>
    public string? InputBlobUri { get; set; }

    /// <summary>
    /// Gets or sets the path to the output Azure storage blob that will contain the errors and progress logs of import job.
    /// </summary>
    public string? OutputBlobUri { get; set; }

    /// <summary>
    /// Gets or sets the overall job status.
    /// </summary>
    public ImportJobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the job creation time (RFC3339 format).
    /// </summary>
    public DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// Gets or sets the last action time (RFC3339 format).
    /// </summary>
    public DateTime LastActionDateTime { get; set; }

    /// <summary>
    /// Gets or sets the job completion time (RFC3339 format).
    /// </summary>
    public DateTime? FinishedDateTime { get; set; }

    /// <summary>
    /// Gets or sets the time at which job will be purged by the service from the system (RFC3339 format).
    /// </summary>
    public DateTime PurgeDateTime { get; set; }

    /// <summary>
    /// Gets or sets details of the error(s) that occurred executing the import job.
    /// </summary>
    public ImportJobError? Error { get; set; }

    // Extended properties for detailed progress tracking (not part of Azure API but useful for internal tracking)
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
    /// Gets or sets the cancellation token source for this job.
    /// </summary>
    public System.Threading.CancellationTokenSource? CancellationTokenSource { get; set; }
}

/// <summary>
/// Error details for import jobs.
/// </summary>
public class ImportJobError
{
    /// <summary>
    /// Gets or sets the service specific error code which serves as the substatus for the HTTP error code.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets a human-readable representation of the error.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets internal error details.
    /// </summary>
    public ImportJobError[]? Details { get; set; }

    /// <summary>
    /// Gets or sets an object containing more specific information than the current object about the error.
    /// </summary>
    public ImportJobInnerError? InnerError { get; set; }
}

/// <summary>
/// Inner error details for import jobs.
/// </summary>
public class ImportJobInnerError
{
    /// <summary>
    /// Gets or sets a more specific error code than was provided by the containing error.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets an object containing more specific information than the current object about the error.
    /// </summary>
    public ImportJobInnerError? InnerError { get; set; }
}

/// <summary>
/// Status of an import job, compatible with Azure Digital Twins API.
/// </summary>
public enum ImportJobStatus
{
    /// <summary>
    /// The job has not started yet.
    /// </summary>
    NotStarted,

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
    /// The job is being cancelled.
    /// </summary>
    Cancelling,

    /// <summary>
    /// The job was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The job completed partially with some errors.
    /// </summary>
    PartiallySucceeded,
}
