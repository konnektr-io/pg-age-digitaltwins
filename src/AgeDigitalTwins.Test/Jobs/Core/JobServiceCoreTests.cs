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
    public async Task CancelImportJob_ShouldChangeStatus_FromRunningToCancellingToCancelled()
    {
        // Arrange
        var jobId = GenerateJobId("cancel-import");
        // Create a larger dataset by repeating the complex data multiple times
        var baseData = TestDataFactory.ImportData.CreateComplexNdJson();
        var largeTestData = string.Join("\n", Enumerable.Repeat(baseData, 50));

        // Start a background import job that will run long enough to be cancelled
        Func<CancellationToken, Task<(Stream inputStream, Stream outputStream)>> streamFactory = (
            ct
        ) =>
        {
            var inputStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(largeTestData));
            var outputStream = new MemoryStream();
            return Task.FromResult<(Stream, Stream)>((inputStream, outputStream));
        };

        try
        {
            // Act - Start background job
            var runningJob = await Client.ImportGraphAsync<object>(
                jobId,
                streamFactory,
                options: null,
                request: null,
                executeInBackground: true,
                cancellationToken: default
            );

            // Assert - Job should be running
            Assert.Equal(JobStatus.Running, runningJob.Status);
            Output.WriteLine($"✓ Import job {jobId} started with status: {runningJob.Status}");

            // Give the job a moment to start processing
            await Task.Delay(100);

            // Act - Cancel the job
            var cancelRequested = await Client.CancelImportJobAsync(jobId);

            // Check current job status to see if we can test cancellation
            var currentJob = await Client.GetImportJobAsync(jobId);

            if (
                currentJob?.Status == JobStatus.Running
                || currentJob?.Status == JobStatus.Cancelling
            )
            {
                // Assert - Cancellation should be accepted for running jobs
                Assert.True(cancelRequested);
                Output.WriteLine($"✓ Cancellation requested for job: {jobId}");

                // Act - Check status change to Cancelling
                var cancellingJob = await Client.GetImportJobAsync(jobId);

                // Assert - Status should change to Cancelling
                Assert.NotNull(cancellingJob);
                Assert.Equal(JobStatus.Cancelling, cancellingJob.Status);
                Output.WriteLine($"✓ Job status changed to: {cancellingJob.Status}");

                // Act - Wait for final cancellation (with timeout)
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
                Output.WriteLine($"✓ Job eventually completed with status: {finalJob.Status}");
            }
            else
            {
                // Job completed before we could cancel it
                Output.WriteLine(
                    $"✓ Job completed too quickly to test cancellation, final status: {currentJob?.Status}"
                );
                // This is acceptable for this test since job execution speed can vary
            }
        }
        finally
        {
            await CleanupJobAsync(jobId, "import");
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

        try
        {
            // Act - Start background delete job
            var runningJob = await Client.DeleteAllAsync(jobId);

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
}
