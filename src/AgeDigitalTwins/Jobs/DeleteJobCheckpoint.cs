using System;
using AgeDigitalTwins.Models;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Checkpoint data for delete jobs to support resumption after failures.
/// </summary>
public class DeleteJobCheckpoint
{
    /// <summary>
    /// The job identifier this checkpoint belongs to.
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// The current section being processed.
    /// </summary>
    public DeleteSection CurrentSection { get; set; }

    /// <summary>
    /// When this checkpoint was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Number of relationships that have been deleted.
    /// </summary>
    public int RelationshipsDeleted { get; set; }

    /// <summary>
    /// Number of twins that have been deleted.
    /// </summary>
    public int TwinsDeleted { get; set; }

    /// <summary>
    /// Number of models that have been deleted.
    /// </summary>
    public int ModelsDeleted { get; set; }

    /// <summary>
    /// Number of errors encountered so far.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Whether the relationships section has been completed.
    /// </summary>
    public bool RelationshipsCompleted { get; set; }

    /// <summary>
    /// Whether the twins section has been completed.
    /// </summary>
    public bool TwinsCompleted { get; set; }

    /// <summary>
    /// Whether the models section has been completed.
    /// </summary>
    public bool ModelsCompleted { get; set; }

    /// <summary>
    /// The last processed batch of relationships (for resumption).
    /// </summary>
    public string? LastProcessedRelationshipBatch { get; set; }

    /// <summary>
    /// The last processed batch of twins (for resumption).
    /// </summary>
    public string? LastProcessedTwinBatch { get; set; }

    /// <summary>
    /// Creates a new checkpoint for the specified job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>A new checkpoint instance.</returns>
    public static DeleteJobCheckpoint Create(string jobId)
    {
        return new DeleteJobCheckpoint
        {
            JobId = jobId,
            CurrentSection = DeleteSection.Relationships,
            LastUpdated = DateTime.UtcNow,
            RelationshipsDeleted = 0,
            TwinsDeleted = 0,
            ModelsDeleted = 0,
            ErrorCount = 0,
            RelationshipsCompleted = false,
            TwinsCompleted = false,
            ModelsCompleted = false,
        };
    }

    /// <summary>
    /// Updates the checkpoint with current progress.
    /// </summary>
    /// <param name="result">The current job result.</param>
    public void UpdateProgress(JobRecord result)
    {
        RelationshipsDeleted = result.RelationshipsDeleted;
        TwinsDeleted = result.TwinsDeleted;
        ModelsDeleted = result.ModelsDeleted;
        ErrorCount = result.ErrorCount;
        LastUpdated = DateTime.UtcNow;
    }
}

/// <summary>
/// Current section enumeration for delete checkpoint tracking.
/// </summary>
public enum DeleteSection
{
    Relationships,
    Twins,
    Models,
    Completed,
}
