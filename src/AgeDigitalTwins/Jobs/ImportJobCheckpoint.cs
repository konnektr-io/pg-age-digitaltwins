using System;
using System.Collections.Generic;
using AgeDigitalTwins.Models;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Checkpoint data for import jobs to support resumption after failures.
/// </summary>
public class ImportJobCheckpoint
{
    /// <summary>
    /// The job identifier this checkpoint belongs to.
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// The current section being processed.
    /// </summary>
    public CurrentSection CurrentSection { get; set; }

    /// <summary>
    /// The current line number in the input stream.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// When this checkpoint was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Number of models that have been processed.
    /// </summary>
    public int ModelsProcessed { get; set; }

    /// <summary>
    /// Number of twins that have been processed.
    /// </summary>
    public int TwinsProcessed { get; set; }

    /// <summary>
    /// Number of relationships that have been processed.
    /// </summary>
    public int RelationshipsProcessed { get; set; }

    /// <summary>
    /// Number of errors encountered so far.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Models that have been collected but not yet processed.
    /// Used for resuming in the middle of model collection.
    /// </summary>
    public List<string> PendingModels { get; set; } = new();

    /// <summary>
    /// Whether the models section has been completed.
    /// </summary>
    public bool ModelsCompleted { get; set; }

    /// <summary>
    /// Whether the twins section has been completed.
    /// </summary>
    public bool TwinsCompleted { get; set; }

    /// <summary>
    /// Whether the relationships section has been completed.
    /// </summary>
    public bool RelationshipsCompleted { get; set; }

    /// <summary>
    /// Creates a new checkpoint for the specified job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>A new checkpoint instance.</returns>
    public static ImportJobCheckpoint Create(string jobId)
    {
        return new ImportJobCheckpoint
        {
            JobId = jobId,
            CurrentSection = CurrentSection.None,
            LineNumber = 0,
            LastUpdated = DateTime.UtcNow,
            ModelsProcessed = 0,
            TwinsProcessed = 0,
            RelationshipsProcessed = 0,
            ErrorCount = 0,
            PendingModels = new List<string>(),
            ModelsCompleted = false,
            TwinsCompleted = false,
            RelationshipsCompleted = false,
        };
    }

    /// <summary>
    /// Updates the checkpoint with current progress.
    /// </summary>
    /// <param name="result">The current job result.</param>
    public void UpdateProgress(JobRecord result)
    {
        ModelsProcessed = result.ModelsCreated;
        TwinsProcessed = result.TwinsCreated;
        RelationshipsProcessed = result.RelationshipsCreated;
        ErrorCount = result.ErrorCount;
        LastUpdated = DateTime.UtcNow;
    }
}

/// <summary>
/// Current section enumeration for checkpoint tracking.
/// </summary>
public enum CurrentSection
{
    None,
    Header,
    Models,
    Twins,
    Relationships,
}
