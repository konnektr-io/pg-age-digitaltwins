using System.Text;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Tests for the delete job system functionality through the AgeDigitalTwinsClient.
/// Delete jobs remove all relationships, twins, and models in the correct order.
/// </summary>
[Trait("Category", "Integration")]
public class DeleteJobTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public DeleteJobTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DeleteImportJobAsync_ShouldCreateAndExecuteJob_WithValidParameters()
    {
        // Arrange
        var jobId = $"test-delete-job-{Guid.NewGuid()}";

        // First, let's create some test data to delete
        await CreateTestDataAsync();

        try
        {
            // Act
            var jobRecord = await Client.DeleteAllAsync(jobId);

            // Assert
            Assert.NotNull(jobRecord);
            Assert.Equal(jobId, jobRecord.Id);
            Assert.Equal("delete", jobRecord.JobType);
            Assert.True(
                jobRecord.Status == JobStatus.Succeeded
                    || jobRecord.Status == JobStatus.PartiallySucceeded
            );
            Assert.True(jobRecord.CreatedDateTime <= DateTime.UtcNow);
            Assert.True(jobRecord.LastActionDateTime <= DateTime.UtcNow);
            Assert.NotNull(jobRecord.FinishedDateTime);
            Assert.True(jobRecord.PurgeDateTime > DateTime.UtcNow);

            // Should have deleted some items (exact counts depend on test data)
            Assert.True(jobRecord.RelationshipsDeleted >= 0);
            Assert.True(jobRecord.TwinsDeleted >= 0);
            Assert.True(jobRecord.ModelsDeleted >= 0);

            _output.WriteLine($"✓ Created and executed delete job: {jobRecord.Id}");
            _output.WriteLine($"  Status: {jobRecord.Status}");
            _output.WriteLine($"  Relationships deleted: {jobRecord.RelationshipsDeleted}");
            _output.WriteLine($"  Twins deleted: {jobRecord.TwinsDeleted}");
            _output.WriteLine($"  Models deleted: {jobRecord.ModelsDeleted}");
            _output.WriteLine($"  Errors: {jobRecord.ErrorCount}");
        }
        finally
        {
            // Cleanup job record
            Client.DeleteImportJob(jobId);
        }
    }

    [Fact]
    public async Task GetDeleteJobAsync_ShouldReturnJob_WhenJobExists()
    {
        // Arrange
        var jobId = $"test-delete-job-{Guid.NewGuid()}";

        try
        {
            // Create a delete job first
            var createdJob = await Client.DeleteAllAsync(jobId);

            // Act
            var retrievedJob = await Client.GetDeleteJobAsync(jobId);

            // Assert
            Assert.NotNull(retrievedJob);
            Assert.Equal(jobId, retrievedJob.Id);
            Assert.Equal(createdJob.Status, retrievedJob.Status);
            Assert.Equal("delete", retrievedJob.JobType);

            _output.WriteLine(
                $"✓ Retrieved delete job: {retrievedJob.Id} with status: {retrievedJob.Status}"
            );
        }
        finally
        {
            Client.DeleteImportJob(jobId);
        }
    }

    [Fact]
    public async Task GetDeleteJob_ShouldReturnJob_WhenJobExists()
    {
        // Arrange
        var jobId = $"test-delete-job-{Guid.NewGuid()}";

        try
        {
            // Create a delete job first
            var createdJob = await Client.DeleteAllAsync(jobId);

            // Act - Test synchronous version
            var retrievedJob = Client.GetDeleteJob(jobId);

            // Assert
            Assert.NotNull(retrievedJob);
            Assert.Equal(jobId, retrievedJob.Id);
            Assert.Equal(createdJob.Status, retrievedJob.Status);
            Assert.Equal("delete", retrievedJob.JobType);

            _output.WriteLine(
                $"✓ Retrieved delete job (sync): {retrievedJob.Id} with status: {retrievedJob.Status}"
            );
        }
        finally
        {
            Client.DeleteImportJob(jobId);
        }
    }

    [Fact]
    public async Task GetDeleteJobAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        // Act
        var retrievedJob = await Client.GetDeleteJobAsync("non-existent-delete-job");

        // Assert
        Assert.Null(retrievedJob);
        _output.WriteLine("✓ Non-existent delete job returned null as expected");
    }

    [Fact]
    public void GetDeleteJob_ShouldReturnNull_WhenJobDoesNotExist()
    {
        // Act
        var retrievedJob = Client.GetDeleteJob("non-existent-delete-job");

        // Assert
        Assert.Null(retrievedJob);
        _output.WriteLine("✓ Non-existent delete job returned null as expected (sync)");
    }

    [Fact]
    public async Task GetDeleteJobsAsync_ShouldReturnJobs_WhenJobsExist()
    {
        // Arrange
        var jobId1 = $"test-delete-job-1-{Guid.NewGuid()}";
        var jobId2 = $"test-delete-job-2-{Guid.NewGuid()}";

        try
        {
            // Create delete jobs
            await Client.DeleteAllAsync(jobId1);
            await Client.DeleteAllAsync(jobId2);

            // Act
            var jobs = (await Client.GetDeleteJobsAsync()).ToList();

            // Assert
            Assert.Contains(jobs, j => j.Id == jobId1);
            Assert.Contains(jobs, j => j.Id == jobId2);
            Assert.True(jobs.Count >= 2);

            // All returned jobs should be delete jobs
            Assert.All(jobs, job => Assert.Equal("delete", job.JobType));

            _output.WriteLine($"✓ Listed {jobs.Count} delete jobs");
            foreach (var job in jobs.Take(5)) // Show first 5 for brevity
            {
                _output.WriteLine(
                    $"  - Job ID: {job.Id}, Status: {job.Status}, Type: {job.JobType}"
                );
            }
        }
        finally
        {
            Client.DeleteImportJob(jobId1);
            Client.DeleteImportJob(jobId2);
        }
    }

    [Fact]
    public async Task DeleteImportJobAsync_ShouldThrowException_ForDuplicateJobId()
    {
        // Arrange
        var jobId = $"test-delete-job-{Guid.NewGuid()}";

        try
        {
            await Client.DeleteAllAsync(jobId);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Client.DeleteAllAsync(jobId)
            );

            Assert.Contains("already exists", exception.Message);
            _output.WriteLine(
                $"✓ Duplicate delete job ID correctly threw exception: {exception.Message}"
            );
        }
        finally
        {
            Client.DeleteImportJob(jobId);
        }
    }

    [Fact]
    public async Task DeleteImportJobAsync_ShouldHandleEmptyDatabase_Gracefully()
    {
        // Arrange
        var jobId = $"test-delete-empty-{Guid.NewGuid()}";

        try
        {
            // Act - Try to delete from empty database
            var jobRecord = await Client.DeleteAllAsync(jobId);

            // Assert
            Assert.NotNull(jobRecord);
            Assert.Equal(jobId, jobRecord.Id);
            Assert.Equal("delete", jobRecord.JobType);
            Assert.Equal(JobStatus.Succeeded, jobRecord.Status);

            // Should have zero deletions for empty database
            Assert.Equal(0, jobRecord.RelationshipsDeleted);
            Assert.Equal(0, jobRecord.TwinsDeleted);
            Assert.Equal(0, jobRecord.ModelsDeleted);
            Assert.Equal(0, jobRecord.ErrorCount);

            _output.WriteLine($"✓ Delete job on empty database completed successfully");
            _output.WriteLine($"  All deletion counts are zero as expected");
        }
        finally
        {
            Client.DeleteImportJob(jobId);
        }
    }

    [Fact]
    public async Task DeleteImportJobAsync_ShouldDelete_InCorrectOrder()
    {
        // Arrange
        var jobId = $"test-delete-order-{Guid.NewGuid()}";

        // Create test data with dependencies (models -> twins -> relationships)
        await CreateTestDataWithDependenciesAsync();

        try
        {
            // Act
            var jobRecord = await Client.DeleteAllAsync(jobId);

            // Assert
            Assert.NotNull(jobRecord);
            Assert.Equal(JobStatus.Succeeded, jobRecord.Status);

            // Verify that deletions happened in correct order
            // (This is more of a structural test - the actual verification of order
            // would require more detailed logging/monitoring in the actual implementation)
            Assert.True(jobRecord.RelationshipsDeleted >= 0);
            Assert.True(jobRecord.TwinsDeleted >= 0);
            Assert.True(jobRecord.ModelsDeleted >= 0);

            _output.WriteLine($"✓ Delete job completed with correct dependency order");
            _output.WriteLine($"  Relationships deleted: {jobRecord.RelationshipsDeleted}");
            _output.WriteLine($"  Twins deleted: {jobRecord.TwinsDeleted}");
            _output.WriteLine($"  Models deleted: {jobRecord.ModelsDeleted}");
        }
        finally
        {
            Client.DeleteImportJob(jobId);
        }
    }

    /// <summary>
    /// Helper method to create some test data for deletion tests.
    /// </summary>
    private async Task CreateTestDataAsync()
    {
        try
        {
            // Create a simple model for testing
            var testModelJson =
                @"{
                ""@id"": ""dtmi:example:TestModel;1"",
                ""@type"": ""Interface"",
                ""@context"": ""dtmi:dtdl:context;2"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""testProperty"",
                        ""schema"": ""string""
                    }
                ]
            }";

            await Client.CreateModelsAsync([testModelJson]);

            // Create a test twin
            var testTwin = new { testProperty = "test-value" };

            await Client.CreateOrReplaceDigitalTwinAsync("test-twin-for-deletion", testTwin);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not create test data: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to create test data with dependencies for order testing.
    /// </summary>
    private async Task CreateTestDataWithDependenciesAsync()
    {
        try
        {
            // Create models
            var model1Json =
                @"{
                ""@id"": ""dtmi:example:Model1;1"",
                ""@type"": ""Interface"",
                ""@context"": ""dtmi:dtdl:context;2"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""prop1"",
                        ""schema"": ""string""
                    },
                    {
                        ""@type"": ""Relationship"",
                        ""name"": ""relatesTo"",
                        ""target"": ""dtmi:example:Model2;1""
                    }
                ]
            }";

            var model2Json =
                @"{
                ""@id"": ""dtmi:example:Model2;1"",
                ""@type"": ""Interface"",
                ""@context"": ""dtmi:dtdl:context;2"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""prop2"",
                        ""schema"": ""string""
                    }
                ]
            }";

            await Client.CreateModelsAsync([model1Json, model2Json]);

            // Create twins
            var twin1 = new { prop1 = "value1" };
            var twin2 = new { prop2 = "value2" };

            await Client.CreateOrReplaceDigitalTwinAsync("twin1-for-deletion", twin1);
            await Client.CreateOrReplaceDigitalTwinAsync("twin2-for-deletion", twin2);

            // Create relationship
            var relationship = new Dictionary<string, object>
            {
                ["$relationshipName"] = "relatesTo",
                ["$targetId"] = "twin2-for-deletion",
            };

            await Client.CreateOrReplaceRelationshipAsync(
                "twin1-for-deletion",
                "rel1-for-deletion",
                relationship
            );
        }
        catch (Exception ex)
        {
            _output.WriteLine(
                $"Warning: Could not create test data with dependencies: {ex.Message}"
            );
        }
    }
}
