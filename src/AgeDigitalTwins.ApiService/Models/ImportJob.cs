using AgeDigitalTwins.Models;

namespace AgeDigitalTwins.ApiService.Models;

/// <summary>
/// Represents an import job with its current status and details.
/// </summary>
public class ImportJob : ImportJobRequest
{
    /// <summary>
    /// Gets or sets the unique identifier for the import job.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets the current status of the import job.
    /// </summary>
    public JobStatus Status { get; init; }

    /// <summary>
    /// Gets the date and time when the import job was created.
    /// </summary>
    public DateTimeOffset CreatedDateTime { get; init; }

    /// <summary>
    /// Gets the date and time when the import job was last updated.
    /// </summary>
    public DateTimeOffset LastActionDateTime { get; init; }

    /// <summary>
    /// Gets the date and time when the import job finished (if completed).
    /// </summary>
    public DateTimeOffset? FinishedDateTime { get; init; }

    /// <summary>
    /// Gets the date and time when the import job will be purged from the system.
    /// </summary>
    public DateTimeOffset? PurgeDateTime { get; init; }

    /// <summary>
    /// Gets the number of models that were successfully created.
    /// </summary>
    public int ModelsCreated { get; init; }

    /// <summary>
    /// Gets the number of twins that were successfully created.
    /// </summary>
    public int TwinsCreated { get; init; }

    /// <summary>
    /// Gets the number of relationships that were successfully created.
    /// </summary>
    public int RelationshipsCreated { get; init; }

    /// <summary>
    /// Gets the number of errors that occurred during the import.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportJob"/> class.
    /// </summary>
    public ImportJob() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportJob"/> class from a job record.
    /// </summary>
    /// <param name="jobRecord">The job record to convert.</param>
    internal ImportJob(JobRecord jobRecord)
    {
        Id = jobRecord.Id;
        Status = jobRecord.Status;
        CreatedDateTime = jobRecord.CreatedAt;
        LastActionDateTime = jobRecord.UpdatedAt;
        FinishedDateTime = jobRecord.FinishedAt;
        PurgeDateTime = jobRecord.PurgeAt;

        // Extract URIs from request data if available
        if (jobRecord.RequestData != null)
        {
            var requestData = jobRecord.RequestData.RootElement;
            if (requestData.TryGetProperty("inputBlobUri", out var inputUri))
                InputBlobUri = new Uri(inputUri.GetString() ?? "about:blank");
            if (requestData.TryGetProperty("outputBlobUri", out var outputUri))
                OutputBlobUri = new Uri(outputUri.GetString() ?? "about:blank");
        }

        // Set placeholder URIs if not found in request data
        if (InputBlobUri == null)
            InputBlobUri = new Uri("about:blank");
        if (OutputBlobUri == null)
            OutputBlobUri = new Uri("about:blank");

        // Use JobRecord's smart properties that read from checkpoint data during execution
        // and result data when completed
        ModelsCreated = jobRecord.ModelsCreated;
        TwinsCreated = jobRecord.TwinsCreated;
        RelationshipsCreated = jobRecord.RelationshipsCreated;
        ErrorCount = jobRecord.ErrorCount;
    }
}
