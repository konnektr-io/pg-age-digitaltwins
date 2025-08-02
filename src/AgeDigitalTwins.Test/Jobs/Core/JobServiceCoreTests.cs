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
}
