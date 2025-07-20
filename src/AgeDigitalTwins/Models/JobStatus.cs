namespace AgeDigitalTwins.Models;

/// <summary>
/// Job status enumeration.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job has been created but not yet started.
    /// </summary>
    Notstarted,

    /// <summary>
    /// Job is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Job completed partially with some errors.
    /// </summary>
    PartiallySucceeded,

    /// <summary>
    /// Job failed to complete.
    /// </summary>
    Failed,

    /// <summary>
    /// Job was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Job cancellation was requested.
    /// </summary>
    Cancelling,
}
