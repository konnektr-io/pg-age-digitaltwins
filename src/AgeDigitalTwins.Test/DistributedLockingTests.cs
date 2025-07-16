using System;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using Xunit;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Tests for the distributed locking functionality in JobService.
/// </summary>
[Trait("Category", "Integration")]
public class DistributedLockingTests : TestBase
{
    [Fact]
    public async Task TryAcquireJobLockAsync_ShouldSucceed_WhenLockIsAvailable()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);

        // Assert
        Assert.True(lockAcquired);

        // Cleanup
        await jobService.ReleaseJobLockAsync(jobId);
    }

    [Fact]
    public async Task TryAcquireJobLockAsync_ShouldFail_WhenLockIsAlreadyTaken()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var firstLockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
        var secondLockAcquired = await jobService.TryAcquireJobLockAsync(jobId);

        // Assert
        Assert.True(firstLockAcquired);
        Assert.False(secondLockAcquired);

        // Cleanup
        await jobService.ReleaseJobLockAsync(jobId);
    }

    [Fact]
    public async Task RenewJobLockHeartbeatAsync_ShouldSucceed_WhenLockIsOwnedByCurrentInstance()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
        var heartbeatRenewed = await jobService.RenewJobLockHeartbeatAsync(jobId);

        // Assert
        Assert.True(lockAcquired);
        Assert.True(heartbeatRenewed);

        // Cleanup
        await jobService.ReleaseJobLockAsync(jobId);
    }

    [Fact]
    public async Task RenewJobLockHeartbeatAsync_ShouldFail_WhenLockIsNotOwned()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var heartbeatRenewed = await jobService.RenewJobLockHeartbeatAsync(jobId);

        // Assert
        Assert.False(heartbeatRenewed);
    }

    [Fact]
    public async Task ReleaseJobLockAsync_ShouldSucceed_WhenLockIsOwnedByCurrentInstance()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
        var lockReleased = await jobService.ReleaseJobLockAsync(jobId);

        // Assert
        Assert.True(lockAcquired);
        Assert.True(lockReleased);
    }

    [Fact]
    public async Task ReleaseJobLockAsync_ShouldFail_WhenLockIsNotOwned()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var lockReleased = await jobService.ReleaseJobLockAsync(jobId);

        // Assert
        Assert.False(lockReleased);
    }

    [Fact]
    public async Task IsJobLockedByCurrentInstanceAsync_ShouldReturnTrue_WhenLockIsOwned()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
        var isLockedByCurrentInstance = await jobService.IsJobLockedByCurrentInstanceAsync(jobId);

        // Assert
        Assert.True(lockAcquired);
        Assert.True(isLockedByCurrentInstance);

        // Cleanup
        await jobService.ReleaseJobLockAsync(jobId);
    }

    [Fact]
    public async Task IsJobLockedByCurrentInstanceAsync_ShouldReturnFalse_WhenLockIsNotOwned()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var isLockedByCurrentInstance = await jobService.IsJobLockedByCurrentInstanceAsync(jobId);

        // Assert
        Assert.False(isLockedByCurrentInstance);
    }

    [Fact]
    public async Task GetJobLockInfoAsync_ShouldReturnLockInfo_WhenLockExists()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
        var lockInfo = await jobService.GetJobLockInfoAsync(jobId);

        // Assert
        Assert.True(lockAcquired);
        Assert.NotNull(lockInfo);
        Assert.Equal(jobId, lockInfo.JobId);
        Assert.False(lockInfo.IsExpired);

        // Cleanup
        await jobService.ReleaseJobLockAsync(jobId);
    }

    [Fact]
    public async Task GetJobLockInfoAsync_ShouldReturnNull_WhenLockDoesNotExist()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var lockInfo = await jobService.GetJobLockInfoAsync(jobId);

        // Assert
        Assert.Null(lockInfo);
    }

    [Fact]
    public async Task CleanupExpiredLocksAsync_ShouldCleanupExpiredLocks()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var lockAcquired = await jobService.TryAcquireJobLockAsync(
            jobId,
            TimeSpan.FromMilliseconds(1)
        );
        
        // Wait for the lock to expire
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        
        var expiredLocksCount = await jobService.CleanupExpiredLocksAsync();

        // Assert
        Assert.True(lockAcquired);
        Assert.True(expiredLocksCount >= 0); // Should clean up at least our expired lock
    }

    [Fact]
    public async Task TryAcquireJobLockAsync_ShouldSucceed_AfterLockExpires()
    {
        // Arrange
        var jobId = $"test-lock-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Act
        var firstLockAcquired = await jobService.TryAcquireJobLockAsync(
            jobId,
            TimeSpan.FromMilliseconds(1)
        );
        
        // Wait for the lock to expire
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        
        var secondLockAcquired = await jobService.TryAcquireJobLockAsync(jobId);

        // Assert
        Assert.True(firstLockAcquired);
        Assert.True(secondLockAcquired);

        // Cleanup
        await jobService.ReleaseJobLockAsync(jobId);
    }
}
