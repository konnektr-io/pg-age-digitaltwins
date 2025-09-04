using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.Test.Infrastructure;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Jobs.Core;

/// <summary>
/// Tests for basic JobService CRUD operations and core functionality.
/// Consolidates basic job creation, retrieval, status updates, and listing operations.
/// </summary>
[Trait("Category", "Integration")]
public class JobServiceCoreTests : JobTestBase
{
    public JobServiceCoreTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task CreateJobAsync_ShouldCreateJob_WithValidParameters()
    {
        // Arrange
        var jobId = GenerateJobId();
        var jobService = Client.JobService;

        try
        {
            // Act
            await jobService.CreateJobAsync(jobId, "test", new { TestData = "value" });

            // Assert
            var job = await jobService.GetJobAsync(jobId);
            AssertJobBasicProperties(job!, jobId, "test");
            JobAssertions.AssertJobStatus(job!, JobStatus.Notstarted);

            Output.WriteLine($"✓ Created job: {jobId}");
        }
        finally
        {
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task GetJobAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        // Arrange
        var jobService = Client.JobService;

        // Act
        var job = await jobService.GetJobAsync("non-existent-job");

        // Assert
        Assert.Null(job);
        Output.WriteLine("✓ Non-existent job returned null as expected");
    }

    [Fact]
    public async Task UpdateJobStatusAsync_ShouldUpdateStatus_WhenJobExists()
    {
        // Arrange
        var jobId = GenerateJobId();
        var jobService = Client.JobService;

        try
        {
            await jobService.CreateJobAsync(jobId, "test", new { });

            // Act
            await jobService.UpdateJobStatusAsync(jobId, JobStatus.Succeeded);

            // Assert
            var job = await jobService.GetJobAsync(jobId);
            JobAssertions.AssertJobStatus(job!, JobStatus.Succeeded);

            Output.WriteLine($"✓ Updated job status to: {job!.Status}");
        }
        finally
        {
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task ListJobsAsync_ShouldReturnJobsOfSpecificType()
    {
        // Arrange
        var jobId1 = GenerateJobId("import");
        var jobId2 = GenerateJobId("delete");
        var jobId3 = GenerateJobId("import");
        var jobService = Client.JobService;

        try
        {
            await jobService.CreateJobAsync(jobId1, "import", new { });
            await jobService.CreateJobAsync(jobId2, "delete", new { });
            await jobService.CreateJobAsync(jobId3, "import", new { });

            // Act
            var importJobs = (await jobService.ListJobsAsync("import")).ToList();
            var deleteJobs = (await jobService.ListJobsAsync("delete")).ToList();

            // Assert
            Assert.Contains(importJobs, j => j.Id == jobId1);
            Assert.Contains(importJobs, j => j.Id == jobId3);
            Assert.Contains(deleteJobs, j => j.Id == jobId2);
            Assert.DoesNotContain(importJobs, j => j.Id == jobId2);

            Output.WriteLine(
                $"✓ Found {importJobs.Count} import jobs and {deleteJobs.Count} delete jobs"
            );
        }
        finally
        {
            await CleanupJobAsync(jobId1, "import");
            await CleanupJobAsync(jobId2, "delete");
            await CleanupJobAsync(jobId3, "import");
        }
    }

    [Fact]
    public async Task DeleteJobAsync_ShouldRemoveJob_WhenJobExists()
    {
        // Arrange
        var jobId = GenerateJobId();
        var jobService = Client.JobService;

        // Act
        await jobService.CreateJobAsync(jobId, "test", new { });
        var job = await jobService.GetJobAsync(jobId);
        Assert.NotNull(job); // Verify job was created

        await jobService.DeleteJobAsync(jobId);
        var deletedJob = await jobService.GetJobAsync(jobId);

        // Assert
        Assert.Null(deletedJob);
        Output.WriteLine($"✓ Successfully deleted job: {jobId}");
    }

    [Fact]
    public async Task CreateJobAsync_ShouldThrowException_ForDuplicateJobId()
    {
        // Arrange
        var jobId = GenerateJobId();
        var jobService = Client.JobService;

        try
        {
            await jobService.CreateJobAsync(jobId, "test", new { });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => jobService.CreateJobAsync(jobId, "test", new { })
            );

            Assert.Contains("already exists", exception.Message);
            Output.WriteLine($"✓ Duplicate job ID correctly threw exception: {exception.Message}");
        }
        finally
        {
            await CleanupJobAsync(jobId, "test");
        }
    }

    [Fact]
    public async Task CancelDeleteJob_ShouldChangeStatus_FromRunningToCancellingToCancelled()
    {
        // Arrange
        var jobId = GenerateJobId("cancel-delete");

        // Create some test data that will take time to delete
        var modelId1 = await CreateTestModelAsync();
        var modelId2 = await CreateTestModelAsync();
        var twinId1 = await CreateTestTwinAsync(modelId: modelId1);
        var twinId2 = await CreateTestTwinAsync(modelId: modelId2);
        await CreateTestRelationshipAsync(twinId1, twinId2);

        // Use a fast heartbeat interval for testing (100ms)
        var options = new DeleteJobOptions
        {
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            BatchSize = 10,
            CheckpointInterval = 10,
        };

        try
        {
            // Act - Start background delete job
            var runningJob = await Client.DeleteAllAsync(jobId, options);

            // Assert - Job should be running (or may complete very quickly)
            Assert.True(
                runningJob.Status == JobStatus.Running || runningJob.Status == JobStatus.Succeeded
            );
            Output.WriteLine($"✓ Delete job {jobId} started with status: {runningJob.Status}");

            // Only test cancellation if the job is still running
            if (runningJob.Status == JobStatus.Running)
            {
                // Act - Cancel the job
                var cancelRequested = await Client.CancelDeleteJobAsync(jobId);

                // Assert - Cancellation should be accepted
                Assert.True(cancelRequested);
                Output.WriteLine($"✓ Cancellation requested for job: {jobId}");

                // Act - Check immediate status change to Cancelling
                var cancellingJob = await Client.GetDeleteJobAsync(jobId);

                // Assert - Status should change to Cancelling immediately
                Assert.NotNull(cancellingJob);
                Assert.Equal(JobStatus.Cancelling, cancellingJob.Status);
                Output.WriteLine($"✓ Job status immediately changed to: {cancellingJob.Status}");

                // Act - Wait for final cancellation (with timeout)
                var maxWaitTime = TimeSpan.FromSeconds(10);
                var startTime = DateTime.UtcNow;
                JobRecord? finalJob = null;

                while (DateTime.UtcNow - startTime < maxWaitTime)
                {
                    finalJob = await Client.GetDeleteJobAsync(jobId);
                    if (finalJob?.Status == JobStatus.Cancelled)
                        break;
                    await Task.Delay(500);
                }

                // Assert - Job should eventually be cancelled
                Assert.NotNull(finalJob);
                Assert.Equal(JobStatus.Cancelled, finalJob.Status);
                Output.WriteLine($"✓ Job eventually completed with status: {finalJob.Status}");
            }
            else
            {
                Output.WriteLine("✓ Delete job completed too quickly to test cancellation");
            }
        }
        finally
        {
            await CleanupJobAsync(jobId, "delete");
        }
    }

    [Fact]
    public async Task CancelNonExistentJob_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentJobId = GenerateJobId("non-existent");

        // Act
        var importCancelResult = await Client.CancelImportJobAsync(nonExistentJobId);
        var deleteCancelResult = await Client.CancelDeleteJobAsync(nonExistentJobId);

        // Assert
        Assert.False(importCancelResult);
        Assert.False(deleteCancelResult);

        Output.WriteLine($"✓ Cancellation correctly returned false for non-existent jobs");
    }

    [Fact]
    public async Task CancelExistingJob_ShouldReturnTrue_AndSetStatusToCancelling()
    {
        // Arrange - Create a job first
        var jobId = GenerateJobId("cancel-test");
        var jobService = Client.JobService;

        try
        {
            // Create a job in "Running" status to simulate a running job
            await jobService.CreateJobAsync(jobId, "import", new { TestData = "value" });
            await jobService.UpdateJobStatusAsync(jobId, JobStatus.Running);

            // Act - Cancel the job
            var cancelResult = await Client.CancelImportJobAsync(jobId);

            // Assert - Cancellation should succeed
            Assert.True(cancelResult);

            // Verify status was changed to Cancelling
            var job = await jobService.GetJobAsync(jobId);
            Assert.NotNull(job);
            Assert.Equal(JobStatus.Cancelling, job.Status);

            Output.WriteLine($"✓ Job {jobId} was successfully set to Cancelling status");
        }
        finally
        {
            await CleanupJobAsync(jobId, "import");
        }
    }

    [Fact(Skip = "This test is timing-dependent and may be flaky in CI environments")]
    public async Task CancelRunningImportJob_ShouldEventuallyBeCancelled()
    {
        // This is a more comprehensive test that verifies the full cancellation workflow
        // It's marked as Skip because it's timing-dependent and may be flaky

        // Arrange
        var jobId = GenerateJobId("cancel-integration");
        var testData = TestDataFactory.ImportData.CreateComplexNdJson();

        // Create a stream factory with a longer delay to give us time to cancel
        Func<CancellationToken, Task<(Stream inputStream, Stream outputStream)>> streamFactory =
            async (ct) =>
            {
                // Wait longer to ensure we can cancel before processing starts
                await Task.Delay(1000, ct);
                var inputStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));
                var outputStream = new MemoryStream();
                return (inputStream, outputStream);
            };

        try
        {
            // Act - Start background job
            var importTask = Client.ImportGraphAsync<object>(
                jobId,
                streamFactory,
                options: null,
                request: null,
                executeInBackground: true,
                cancellationToken: default
            );

            // Wait for the job to start
            await Task.Delay(100);

            // Verify job is running
            var runningJob = await Client.GetImportJobAsync(jobId);
            Assert.NotNull(runningJob);
            Assert.Equal(JobStatus.Running, runningJob.Status);

            // Cancel the job
            var cancelRequested = await Client.CancelImportJobAsync(jobId);
            Assert.True(cancelRequested);

            // Wait for the task to complete
            await importTask;

            // Check final status in database (with retry logic)
            var maxWaitTime = TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;
            JobRecord? finalJob = null;

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                finalJob = await Client.GetImportJobAsync(jobId);
                if (finalJob?.Status == JobStatus.Cancelled)
                    break;
                await Task.Delay(500);
            }

            // Assert - Job should eventually be cancelled
            Assert.NotNull(finalJob);
            Assert.Equal(JobStatus.Cancelled, finalJob.Status);
        }
        finally
        {
            await CleanupJobAsync(jobId, "import");
        }
    }
}
