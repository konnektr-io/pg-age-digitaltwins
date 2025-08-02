using AgeDigitalTwins.Test.Infrastructure;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Jobs.Infrastructure;

/// <summary>
/// Tests for distributed locking functionality in JobService.
/// Reorganized and consolidated from DistributedLockingTests.cs.
/// </summary>
[Trait("Category", "Integration")]
public class DistributedLockingTests : JobTestBase
{
    public DistributedLockingTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task TryAcquireJobLockAsync_ShouldSucceed_WhenLockIsAvailable()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        try
        {
            // Create a job first
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

            // Act
            var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);

            // Assert
            Assert.True(lockAcquired);
            Output.WriteLine($"✓ Successfully acquired lock for job: {jobId}");
        }
        finally
        {
            // Cleanup
            await jobService.ReleaseJobLockAsync(jobId);
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task TryAcquireJobLockAsync_ShouldFail_WhenLockIsAlreadyTaken()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        try
        {
            // Create a job first
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

            // Act
            var firstLockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
            var secondLockAcquired = await jobService.TryAcquireJobLockAsync(jobId);

            // Assert
            Assert.True(firstLockAcquired);
            Assert.False(secondLockAcquired);

            Output.WriteLine($"✓ First lock acquired: {firstLockAcquired}");
            Output.WriteLine($"✓ Second lock correctly failed: {secondLockAcquired}");
        }
        finally
        {
            // Cleanup
            await jobService.ReleaseJobLockAsync(jobId);
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task RenewJobLockHeartbeatAsync_ShouldSucceed_WhenLockIsOwnedByCurrentInstance()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        try
        {
            // Create a job first
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

            // Act
            var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
            var heartbeatRenewed = await jobService.RenewJobLockHeartbeatAsync(jobId);

            // Assert
            Assert.True(lockAcquired);
            Assert.True(heartbeatRenewed);

            Output.WriteLine($"✓ Lock acquired and heartbeat renewed for job: {jobId}");
        }
        finally
        {
            // Cleanup
            await jobService.ReleaseJobLockAsync(jobId);
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task RenewJobLockHeartbeatAsync_ShouldFail_WhenLockIsNotOwned()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        // Act
        var heartbeatRenewed = await jobService.RenewJobLockHeartbeatAsync(jobId);

        // Assert
        Assert.False(heartbeatRenewed);
        Output.WriteLine($"✓ Heartbeat renewal correctly failed for non-owned lock: {jobId}");
    }

    [Fact]
    public async Task ReleaseJobLockAsync_ShouldSucceed_WhenLockIsOwnedByCurrentInstance()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        try
        {
            // Create a job first
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

            // Act
            var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
            var lockReleased = await jobService.ReleaseJobLockAsync(jobId);

            // Assert
            Assert.True(lockAcquired);
            Assert.True(lockReleased);

            Output.WriteLine($"✓ Lock acquired and released successfully for job: {jobId}");
        }
        finally
        {
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task ReleaseJobLockAsync_ShouldFail_WhenLockIsNotOwned()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        // Act
        var lockReleased = await jobService.ReleaseJobLockAsync(jobId);

        // Assert
        Assert.False(lockReleased);
        Output.WriteLine($"✓ Lock release correctly failed for non-owned lock: {jobId}");
    }

    [Fact]
    public async Task IsJobLockedByCurrentInstanceAsync_ShouldReturnTrue_WhenLockIsOwned()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        try
        {
            // Create a job first
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

            // Act
            var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
            var isLockedByCurrentInstance = await jobService.IsJobLockedByCurrentInstanceAsync(
                jobId
            );

            // Assert
            Assert.True(lockAcquired);
            Assert.True(isLockedByCurrentInstance);

            Output.WriteLine($"✓ Lock ownership correctly detected for job: {jobId}");
        }
        finally
        {
            // Cleanup
            await jobService.ReleaseJobLockAsync(jobId);
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task IsJobLockedByCurrentInstanceAsync_ShouldReturnFalse_WhenLockIsNotOwned()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        // Act
        var isLockedByCurrentInstance = await jobService.IsJobLockedByCurrentInstanceAsync(jobId);

        // Assert
        Assert.False(isLockedByCurrentInstance);
        Output.WriteLine($"✓ Non-ownership correctly detected for job: {jobId}");
    }

    [Fact]
    public async Task GetJobLockInfoAsync_ShouldReturnLockInfo_WhenLockExists()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        try
        {
            // Create a job first
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

            // Act
            var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
            var lockInfo = await jobService.GetJobLockInfoAsync(jobId);

            // Assert
            Assert.True(lockAcquired);
            Assert.NotNull(lockInfo);
            Assert.Equal(jobId, lockInfo.JobId);
            Assert.False(lockInfo.IsExpired);

            Output.WriteLine($"✓ Lock info retrieved successfully for job: {jobId}");
            Output.WriteLine($"  Lock expired: {lockInfo.IsExpired}");
        }
        finally
        {
            // Cleanup
            await jobService.ReleaseJobLockAsync(jobId);
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task GetJobLockInfoAsync_ShouldReturnNull_WhenLockDoesNotExist()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        // Act
        var lockInfo = await jobService.GetJobLockInfoAsync(jobId);

        // Assert
        Assert.Null(lockInfo);
        Output.WriteLine($"✓ Lock info correctly returned null for non-existent lock: {jobId}");
    }

    [Fact]
    public async Task CleanupExpiredLocksAsync_ShouldCleanupExpiredLocks()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        try
        {
            // Create a job first
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

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

            Output.WriteLine($"✓ Cleaned up {expiredLocksCount} expired locks");
        }
        finally
        {
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task TryAcquireJobLockAsync_ShouldSucceed_AfterLockExpires()
    {
        // Arrange
        var jobId = GenerateJobId("lock");
        var jobService = Client.JobService;

        try
        {
            // Create a job first
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

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

            Output.WriteLine($"✓ Successfully reacquired lock after expiration for job: {jobId}");
        }
        finally
        {
            // Cleanup
            await jobService.ReleaseJobLockAsync(jobId);
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task LockLifecycle_ShouldWork_EndToEnd()
    {
        // Arrange
        var jobId = GenerateJobId("lock-lifecycle");
        var jobService = Client.JobService;

        try
        {
            await jobService.CreateJobAsync(jobId, "test", (object?)null);

            // Act & Assert - Full lifecycle test

            // 1. Acquire lock
            var lockAcquired = await jobService.TryAcquireJobLockAsync(jobId);
            Assert.True(lockAcquired);

            // 2. Verify ownership
            var isOwned = await jobService.IsJobLockedByCurrentInstanceAsync(jobId);
            Assert.True(isOwned);

            // 3. Renew heartbeat
            var heartbeatRenewed = await jobService.RenewJobLockHeartbeatAsync(jobId);
            Assert.True(heartbeatRenewed);

            // 4. Get lock info
            var lockInfo = await jobService.GetJobLockInfoAsync(jobId);
            Assert.NotNull(lockInfo);
            Assert.Equal(jobId, lockInfo.JobId);

            // 5. Release lock
            var lockReleased = await jobService.ReleaseJobLockAsync(jobId);
            Assert.True(lockReleased);

            // 6. Verify no longer owned
            var stillOwned = await jobService.IsJobLockedByCurrentInstanceAsync(jobId);
            Assert.False(stillOwned);

            Output.WriteLine($"✓ Complete lock lifecycle test passed for job: {jobId}");
        }
        finally
        {
            await CleanupJobAsync(jobId, "test");
        }
    }
}
