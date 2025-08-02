using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Infrastructure;

/// <summary>
/// Base class for delete job tests providing specialized functionality for delete operations.
/// </summary>
public abstract class DeleteJobTestBase : JobTestBase
{
    protected DeleteJobTestBase(ITestOutputHelper output)
        : base(output) { }

    /// <summary>
    /// Creates test data specifically for deletion tests with models, twins, and relationships.
    /// </summary>
    protected async Task CreateTestDataForDeletionAsync()
    {
        try
        {
            // Create models with relationships
            var model1Id = "dtmi:example:DeleteTestModel1;1";
            var model2Id = "dtmi:example:DeleteTestModel2;1";

            var model1 = TestDataFactory.Models.CreateModelWithRelationship(model1Id, model2Id);
            var model2 = TestDataFactory.Models.CreateSimpleModel(model2Id);

            await Client.CreateModelsAsync(new[] { model1, model2 });
            Output.WriteLine($"Created test models: {model1Id}, {model2Id}");

            // Create twins
            var twin1Id = "test-twin-1-for-deletion";
            var twin2Id = "test-twin-2-for-deletion";

            var twin1Json = TestDataFactory.Twins.CreateTwinJson(twin1Id, model1Id);
            var twin2Json = TestDataFactory.Twins.CreateTwinJson(twin2Id, model2Id);

            await Client.CreateOrReplaceDigitalTwinAsync(twin1Id, twin1Json);
            await Client.CreateOrReplaceDigitalTwinAsync(twin2Id, twin2Json);
            Output.WriteLine($"Created test twins: {twin1Id}, {twin2Id}");

            // Create relationship
            var relationshipId = "test-relationship-for-deletion";
            await CreateTestRelationshipAsync(twin1Id, twin2Id, relationshipId);

            Output.WriteLine("✓ Created comprehensive test data for deletion");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Warning: Could not create test data for deletion: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates minimal test data for deletion tests.
    /// </summary>
    protected async Task CreateMinimalTestDataAsync()
    {
        try
        {
            var modelId = await CreateTestModelAsync("dtmi:example:MinimalDeleteTest;1");
            await CreateTestTwinAsync("minimal-test-twin", modelId);

            Output.WriteLine("✓ Created minimal test data for deletion");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Warning: Could not create minimal test data: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a delete job and returns the result.
    /// </summary>
    protected async Task<JobRecord> ExecuteDeleteJobAsync(string? jobId = null)
    {
        jobId ??= GenerateJobId("test-delete");

        var result = await Client.DeleteAllAsync(jobId);
        LogJobResult(result);

        return result;
    }

    /// <summary>
    /// Asserts delete job results with expected counts.
    /// </summary>
    protected void AssertDeleteResults(
        JobRecord job,
        int expectedModels = -1,
        int expectedTwins = -1,
        int expectedRelationships = -1
    )
    {
        AssertJobSuccess(job);
        JobAssertions.AssertDeleteCountsNonNegative(job);

        if (expectedModels >= 0)
            Assert.Equal(expectedModels, job.ModelsDeleted);
        if (expectedTwins >= 0)
            Assert.Equal(expectedTwins, job.TwinsDeleted);
        if (expectedRelationships >= 0)
            Assert.Equal(expectedRelationships, job.RelationshipsDeleted);
    }

    /// <summary>
    /// Asserts that delete job handled empty database gracefully.
    /// </summary>
    protected void AssertEmptyDatabaseHandling(JobRecord job)
    {
        AssertJobBasicProperties(job, job.Id, "delete");
        JobAssertions.AssertJobStatus(job, JobStatus.Succeeded);
        JobAssertions.AssertDeleteCounts(job, 0, 0, 0);
        JobAssertions.AssertNoErrors(job);
    }

    /// <summary>
    /// Validates that a checkpoint has expected values for delete operations.
    /// </summary>
    protected void AssertDeleteCheckpoint(DeleteJobCheckpoint checkpoint, string expectedJobId)
    {
        Assert.NotNull(checkpoint);
        Assert.Equal(expectedJobId, checkpoint.JobId);
        Assert.True(checkpoint.LastUpdated <= DateTime.UtcNow);

        // Validate counts are non-negative
        Assert.True(checkpoint.RelationshipsDeleted >= 0);
        Assert.True(checkpoint.TwinsDeleted >= 0);
        Assert.True(checkpoint.ModelsDeleted >= 0);
        Assert.True(checkpoint.ErrorCount >= 0);
    }

    /// <summary>
    /// Cleans up a delete job.
    /// </summary>
    protected async Task CleanupDeleteJobAsync(string jobId)
    {
        await CleanupJobAsync(jobId, "delete");
    }
}
