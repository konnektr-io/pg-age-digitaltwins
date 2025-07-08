using System;
using System.Text.Json;
using AgeDigitalTwins.Models;

namespace AgeDigitalTwins.Jobs.Models;

/// <summary>
/// Request model for creating an import job.
/// </summary>
/// <summary> A job which contains a reference to the operations to perform, results, and execution metadata. </summary>
public partial class ImportJob
{
    internal ImportJob(JobRecord jobRecord)
    {
        Id = jobRecord.Id;
        InputBlobUri = new Uri(
            jobRecord.RequestData?.RootElement.GetProperty("inputBlobUri").GetString()
                ?? string.Empty
        );
        OutputBlobUri = new Uri(
            jobRecord.RequestData?.RootElement.GetProperty("outputBlobUri").GetString()
                ?? string.Empty
        );
        CreatedDateTime = jobRecord.CreatedAt;
        LastActionDateTime = jobRecord.UpdatedAt;
        FinishedDateTime = jobRecord.FinishedAt;
        PurgeDateTime = jobRecord.PurgeAt;
    }

    internal JobRecord ToJobRecord()
    {
        return new JobRecord
        {
            Id = Id,
            JobType = "import",
            Status = JobStatus.NotStarted,
            CreatedAt = CreatedDateTime ?? DateTimeOffset.UtcNow,
            UpdatedAt = LastActionDateTime ?? DateTimeOffset.UtcNow,
            FinishedAt = FinishedDateTime,
            PurgeAt = PurgeDateTime,
            RequestData = JsonDocument.Parse(
                JsonSerializer.Serialize(
                    new
                    {
                        inputBlobUri = InputBlobUri.ToString(),
                        outputBlobUri = OutputBlobUri.ToString(),
                    }
                )
            ),
        };
    }

    /// <summary> The identifier of the import job. </summary>
    public required string Id { get; set; }

    public required Uri InputBlobUri { get; set; }

    public required Uri OutputBlobUri { get; set; }

    /// <summary> Start time of the job. The timestamp is in RFC3339 format: `yyyy-MM-ddTHH:mm:ssZ`. </summary>
    public DateTimeOffset? CreatedDateTime { get; }

    /// <summary> Last time service performed any action from the job. The timestamp is in RFC3339 format: `yyyy-MM-ddTHH:mm:ssZ`. </summary>
    public DateTimeOffset? LastActionDateTime { get; }

    /// <summary> End time of the job. The timestamp is in RFC3339 format: `yyyy-MM-ddTHH:mm:ssZ`. </summary>
    public DateTimeOffset? FinishedDateTime { get; }

    /// <summary> Time at which job will be purged by the service from the system. The timestamp is in RFC3339 format: `yyyy-MM-ddTHH:mm:ssZ`. </summary>
    public DateTimeOffset? PurgeDateTime { get; }
}
