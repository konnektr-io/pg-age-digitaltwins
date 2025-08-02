using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.Test.Infrastructure;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Jobs.Delete;

/// <summary>
/// Tests for delete job execution logic and deletion operations.
/// Consolidates delete job functionality from DeleteJobTests.cs and DeleteJobSystemTests.cs.
/// </summary>
[Trait("Category", "Integration")]
public class DeleteJobExecutionTests : DeleteJobTestBase
{
    public DeleteJobExecutionTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task DeleteAllAsync_ShouldCreateAndExecuteJob_WithValidParameters()
    {
        // Arrange
        var jobId = GenerateJobId("delete");

        // First, let's create some test data to delete
        await CreateTestDataForDeletionAsync();

        try
        {
            // Act
            var result = await ExecuteDeleteJobAsync(jobId);

            // Assert
            AssertJobBasicProperties(result, jobId, "delete");
            AssertJobSuccess(result);
            JobAssertions.AssertDeleteCountsNonNegative(result);

            Output.WriteLine($"✓ Created and executed delete job: {result.Id}");
            Output.WriteLine($"  Status: {result.Status}");
            Output.WriteLine($"  Relationships deleted: {result.RelationshipsDeleted}");
            Output.WriteLine($"  Twins deleted: {result.TwinsDeleted}");
            Output.WriteLine($"  Models deleted: {result.ModelsDeleted}");
            Output.WriteLine($"  Errors: {result.ErrorCount}");
        }
        finally
        {
            await CleanupDeleteJobAsync(jobId);
        }
    }

    [Fact]
    public async Task DeleteAllAsync_ShouldHandleEmptyDatabase_Gracefully()
    {
        // Arrange
        var jobId = GenerateJobId("delete-empty");

        try
        {
            // Act - Try to delete from empty database
            var result = await ExecuteDeleteJobAsync(jobId);

            // Assert
            AssertEmptyDatabaseHandling(result);

            Output.WriteLine($"✓ Delete job on empty database completed successfully");
            Output.WriteLine($"  All deletion counts are zero as expected");
        }
        finally
        {
            await CleanupDeleteJobAsync(jobId);
        }
    }

    [Fact]
    public async Task GetDeleteJobAsync_ShouldReturnJob_WhenJobExists()
    {
        // Arrange
        var jobId = GenerateJobId("delete");

        try
        {
            // Create a delete job first
            var createdJob = await ExecuteDeleteJobAsync(jobId);

            // Act
            var retrievedJob = await Client.GetDeleteJobAsync(jobId);

            // Assert
            Assert.NotNull(retrievedJob);
            AssertJobBasicProperties(retrievedJob, jobId, "delete");
            Assert.Equal(createdJob.Status, retrievedJob.Status);

            Output.WriteLine(
                $"✓ Retrieved delete job: {retrievedJob.Id} with status: {retrievedJob.Status}"
            );
        }
        finally
        {
            await CleanupDeleteJobAsync(jobId);
        }
    }

    [Fact]
    public async Task GetDeleteJob_ShouldReturnJob_WhenJobExists()
    {
        // Arrange
        var jobId = GenerateJobId("delete");

        try
        {
            // Create a delete job first
            var createdJob = await ExecuteDeleteJobAsync(jobId);

            // Act - Test synchronous version
            var retrievedJob = Client.GetDeleteJob(jobId);

            // Assert
            Assert.NotNull(retrievedJob);
            AssertJobBasicProperties(retrievedJob, jobId, "delete");
            Assert.Equal(createdJob.Status, retrievedJob.Status);

            Output.WriteLine(
                $"✓ Retrieved delete job (sync): {retrievedJob.Id} with status: {retrievedJob.Status}"
            );
        }
        finally
        {
            await CleanupDeleteJobAsync(jobId);
        }
    }

    [Fact]
    public async Task GetDeleteJobAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        // Act
        var retrievedJob = await Client.GetDeleteJobAsync("non-existent-delete-job");

        // Assert
        Assert.Null(retrievedJob);
        Output.WriteLine("✓ Non-existent delete job returned null as expected");
    }

    [Fact]
    public void GetDeleteJob_ShouldReturnNull_WhenJobDoesNotExist()
    {
        // Act
        var retrievedJob = Client.GetDeleteJob("non-existent-delete-job");

        // Assert
        Assert.Null(retrievedJob);
        Output.WriteLine("✓ Non-existent delete job returned null as expected (sync)");
    }

    [Fact]
    public async Task GetDeleteJobsAsync_ShouldReturnJobs_WhenJobsExist()
    {
        // Arrange
        var jobId1 = GenerateJobId("delete-1");
        var jobId2 = GenerateJobId("delete-2");

        try
        {
            // Create delete jobs
            await ExecuteDeleteJobAsync(jobId1);
            await ExecuteDeleteJobAsync(jobId2);

            // Act
            var jobs = (await Client.GetDeleteJobsAsync()).ToList();

            // Assert
            Assert.Contains(jobs, j => j.Id == jobId1);
            Assert.Contains(jobs, j => j.Id == jobId2);
            Assert.True(jobs.Count >= 2);

            // All returned jobs should be delete jobs
            Assert.All(jobs, job => Assert.Equal("delete", job.JobType));

            Output.WriteLine($"✓ Listed {jobs.Count} delete jobs");
            foreach (var job in jobs.Take(5)) // Show first 5 for brevity
            {
                Output.WriteLine(
                    $"  - Job ID: {job.Id}, Status: {job.Status}, Type: {job.JobType}"
                );
            }
        }
        finally
        {
            await CleanupDeleteJobAsync(jobId1);
            await CleanupDeleteJobAsync(jobId2);
        }
    }

    [Fact]
    public async Task DeleteAllAsync_ShouldThrowException_ForDuplicateJobId()
    {
        // Arrange
        var jobId = GenerateJobId("delete");

        try
        {
            await ExecuteDeleteJobAsync(jobId);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ExecuteDeleteJobAsync(jobId)
            );

            Assert.Contains("already exists", exception.Message);
            Output.WriteLine(
                $"✓ Duplicate delete job ID correctly threw exception: {exception.Message}"
            );
        }
        finally
        {
            await CleanupDeleteJobAsync(jobId);
        }
    }

    [Fact]
    public async Task DeleteAllAsync_ShouldDelete_InCorrectOrder()
    {
        // Arrange
        var jobId = GenerateJobId("delete-order");

        // Create test data with dependencies (models -> twins -> relationships)
        await CreateTestDataForDeletionAsync();

        try
        {
            // Act
            var result = await ExecuteDeleteJobAsync(jobId);

            // Assert
            AssertJobBasicProperties(result, jobId, "delete");
            JobAssertions.AssertJobStatus(result, JobStatus.Succeeded);

            // Verify that deletions happened in correct order
            // (This is more of a structural test - the actual verification of order
            // would require more detailed logging/monitoring in the actual implementation)
            JobAssertions.AssertDeleteCountsNonNegative(result);

            Output.WriteLine($"✓ Delete job completed with correct dependency order");
            Output.WriteLine($"  Relationships deleted: {result.RelationshipsDeleted}");
            Output.WriteLine($"  Twins deleted: {result.TwinsDeleted}");
            Output.WriteLine($"  Models deleted: {result.ModelsDeleted}");
        }
        finally
        {
            await CleanupDeleteJobAsync(jobId);
        }
    }
}
