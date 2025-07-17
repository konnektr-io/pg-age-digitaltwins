using System;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Information about a job lock.
/// </summary>
public class JobLockInfo
{
    /// <summary>
    /// The job identifier.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp when the lock was acquired.
    /// </summary>
    public DateTime LockAcquiredAt { get; set; }

    /// <summary>
    /// The instance identifier that acquired the lock.
    /// </summary>
    public string LockAcquiredBy { get; set; } = string.Empty;

    /// <summary>
    /// The duration of the lock lease.
    /// </summary>
    public TimeSpan LockLeaseDuration { get; set; }

    /// <summary>
    /// The timestamp of the last heartbeat, if any.
    /// </summary>
    public DateTime? LockHeartbeatAt { get; set; }

    /// <summary>
    /// The timestamp when the lock expires.
    /// </summary>
    public DateTime LockExpiresAt { get; set; }

    /// <summary>
    /// Whether the lock has expired.
    /// </summary>
    public bool IsExpired { get; set; }
}
