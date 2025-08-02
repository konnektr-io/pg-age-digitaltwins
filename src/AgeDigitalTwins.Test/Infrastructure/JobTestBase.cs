using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Infrastructure;

/// <summary>
/// Base class for all job-related tests providing common functionality and helper methods.
/// </summary>
public abstract class JobTestBase : TestBase
{
    protected readonly ITestOutputHelper Output;

    protected JobTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>
    /// Creates a test model with the specified ID or generates a default one.
    /// </summary>
    protected async Task<string> CreateTestModelAsync(string? modelId = null)
    {
        modelId ??= $"dtmi:example:TestModel{Guid.NewGuid():N};1";

        var testModelJson = TestDataFactory.Models.CreateSimpleModel(modelId);
        await Client.CreateModelsAsync(new[] { testModelJson });

        Output.WriteLine($"Created test model: {modelId}");
        return modelId;
    }

    /// <summary>
    /// Creates a test twin with the specified ID and model, or generates defaults.
    /// </summary>
    protected async Task<string> CreateTestTwinAsync(string? twinId = null, string? modelId = null)
    {
        twinId ??= $"test-twin-{Guid.NewGuid():N}";
        modelId ??= await CreateTestModelAsync();

        var testTwinJson = TestDataFactory.Twins.CreateTwinJson(twinId, modelId);
        await Client.CreateOrReplaceDigitalTwinAsync(twinId, testTwinJson);

        Output.WriteLine($"Created test twin: {twinId} with model: {modelId}");
        return twinId;
    }

    /// <summary>
    /// Creates a test relationship between two twins.
    /// </summary>
    protected async Task<string> CreateTestRelationshipAsync(
        string sourceId,
        string targetId,
        string? relationshipId = null,
        string relationshipName = "relatesTo"
    )
    {
        relationshipId ??= $"rel-{Guid.NewGuid():N}";

        var relationship = new Dictionary<string, object>
        {
            ["$relationshipName"] = relationshipName,
            ["$targetId"] = targetId,
        };

        await Client.CreateOrReplaceRelationshipAsync(sourceId, relationshipId, relationship);

        Output.WriteLine(
            $"Created test relationship: {relationshipId} from {sourceId} to {targetId}"
        );
        return relationshipId;
    }

    /// <summary>
    /// Asserts basic job properties are correct.
    /// </summary>
    protected void AssertJobBasicProperties(JobRecord job, string expectedId, string expectedType)
    {
        JobAssertions.AssertJobBasicProperties(job, expectedId, expectedType);
        Output.WriteLine($"✓ Job {job.Id} has correct basic properties");
    }

    /// <summary>
    /// Asserts that a job completed successfully.
    /// </summary>
    protected void AssertJobSuccess(JobRecord job)
    {
        JobAssertions.AssertJobSuccess(job);
        Output.WriteLine($"✓ Job {job.Id} completed successfully with status: {job.Status}");
    }

    /// <summary>
    /// Cleans up a job by deleting it from the appropriate job service.
    /// </summary>
    protected async Task CleanupJobAsync(string jobId, string jobType)
    {
        try
        {
            switch (jobType.ToLowerInvariant())
            {
                case "import":
                    Client.DeleteImportJob(jobId);
                    break;
                case "delete":
                    Client.DeleteDeleteJob(jobId);
                    break;
                default:
                    await Client.JobService.DeleteJobAsync(jobId);
                    break;
            }
            Output.WriteLine($"✓ Cleaned up job: {jobId}");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Warning: Failed to cleanup job {jobId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a unique job ID with an optional prefix.
    /// </summary>
    protected string GenerateJobId(string prefix = "test-job")
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Logs job execution results for debugging.
    /// </summary>
    protected void LogJobResult(JobRecord job)
    {
        Output.WriteLine($"Job Result:");
        Output.WriteLine($"  ID: {job.Id}");
        Output.WriteLine($"  Type: {job.JobType}");
        Output.WriteLine($"  Status: {job.Status}");
        Output.WriteLine($"  Created: {job.CreatedDateTime}");
        Output.WriteLine($"  Last Action: {job.LastActionDateTime}");
        Output.WriteLine($"  Finished: {job.FinishedDateTime}");
        Output.WriteLine($"  Purge: {job.PurgeDateTime}");

        if (job.JobType == "import")
        {
            Output.WriteLine($"  Models Created: {job.ModelsCreated}");
            Output.WriteLine($"  Twins Created: {job.TwinsCreated}");
            Output.WriteLine($"  Relationships Created: {job.RelationshipsCreated}");
        }
        else if (job.JobType == "delete")
        {
            Output.WriteLine($"  Models Deleted: {job.ModelsDeleted}");
            Output.WriteLine($"  Twins Deleted: {job.TwinsDeleted}");
            Output.WriteLine($"  Relationships Deleted: {job.RelationshipsDeleted}");
        }

        Output.WriteLine($"  Errors: {job.ErrorCount}");

        // Log error details if there's an error
        if (job.Error != null)
        {
            Output.WriteLine($"  Error Details:");
            Output.WriteLine($"    Code: {job.Error.Code}");
            Output.WriteLine($"    Message: {job.Error.Message}");
            if (job.Error.Details != null && job.Error.Details.Count > 0)
            {
                Output.WriteLine($"    Details:");
                foreach (var detail in job.Error.Details)
                {
                    Output.WriteLine($"      {detail.Key}: {detail.Value}");
                }
            }
        }
    }
}
