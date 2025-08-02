using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.Test.Infrastructure;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Jobs.Import;

/// <summary>
/// Tests for import job execution logic and data processing.
/// Consolidates import job functionality from ImportJobTests.cs and ImportJobSystemTests.cs.
/// </summary>
[Trait("Category", "Integration")]
public class ImportJobExecutionTests : ImportJobTestBase
{
    public ImportJobExecutionTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task ImportGraphAsync_ShouldCreateAndExecuteJob_WithValidParameters()
    {
        // Arrange
        var jobId = GenerateJobId("import");

        try
        {
            // Act
            var result = await ExecuteImportJobAsync(jobId);

            // Assert
            AssertJobBasicProperties(result, jobId, "import");
            AssertImportResults(
                result,
                expectedModels: 2,
                expectedTwins: 2,
                expectedRelationships: 1
            );
            JobAssertions.AssertNoErrors(result);
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Fact]
    public async Task ImportGraphAsync_WithValidNdJsonData_ShouldImportSuccessfully()
    {
        // Arrange - Use comprehensive test data
        var jobId = GenerateJobId("import");
        var testData = TestDataFactory.ImportData.CreateValidNdJson();

        try
        {
            // Act
            var result = await ExecuteImportJobAsync(jobId, testData);

            // Assert
            AssertJobBasicProperties(result, jobId, "import");
            JobAssertions.AssertJobStatus(result, JobStatus.Succeeded);
            AssertImportResults(
                result,
                expectedModels: 2,
                expectedTwins: 2,
                expectedRelationships: 1
            );
            JobAssertions.AssertNoErrors(result);
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Fact]
    public async Task ImportGraphAsync_WithOnlyModelsSection_ShouldSucceed()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        var modelsOnlyData = TestDataFactory.ImportData.CreateModelsOnly();

        try
        {
            // Act
            var result = await ExecuteImportJobAsync(jobId, modelsOnlyData);

            // Assert
            AssertJobBasicProperties(result, jobId, "import");
            JobAssertions.AssertJobStatus(result, JobStatus.Succeeded);
            AssertImportResults(
                result,
                expectedModels: 2,
                expectedTwins: 0,
                expectedRelationships: 0
            );
            JobAssertions.AssertNoErrors(result);
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Fact]
    public async Task ImportGraphAsync_WithContinueOnFailureEnabled_ShouldContinueProcessing()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        var dataWithErrors = TestDataFactory.ImportData.CreateInvalidNdJson();
        var options = CreateErrorTestingOptions();

        try
        {
            // Act
            var result = await ExecuteImportJobAsync(jobId, dataWithErrors, options);

            // Assert
            AssertJobBasicProperties(result, jobId, "import");
            JobAssertions.AssertJobStatus(result, JobStatus.PartiallySucceeded);

            // Should have created the valid model but failed on invalid twin
            Assert.True(result.ModelsCreated >= 1);
            Assert.True(result.ErrorCount > 0);

            Output.WriteLine($"Import completed with status: {result.Status}");
            Output.WriteLine(
                $"Models Created: {result.ModelsCreated}, Errors: {result.ErrorCount}"
            );
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Fact]
    public async Task GetImportJob_ShouldReturnJob_WhenJobExists()
    {
        // Arrange
        var jobId = GenerateJobId("import");

        try
        {
            // Create a job first
            var createdJob = await ExecuteImportJobAsync(jobId);

            // Act
            var retrievedJob = Client.GetImportJob(jobId);

            // Assert
            Assert.NotNull(retrievedJob);
            AssertJobBasicProperties(retrievedJob, jobId, "import");
            Assert.Equal(createdJob.Status, retrievedJob.Status);

            Output.WriteLine(
                $"✓ Retrieved job: {retrievedJob.Id} with status: {retrievedJob.Status}"
            );
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Fact]
    public void GetImportJob_ShouldReturnNull_WhenJobDoesNotExist()
    {
        // Act
        var retrievedJob = Client.GetImportJob("non-existent-job");

        // Assert
        Assert.Null(retrievedJob);
        Output.WriteLine("✓ Non-existent job returned null as expected");
    }

    [Fact]
    public async Task GetImportJobsAsync_ShouldReturnJobs_WhenJobsExist()
    {
        // Arrange
        var jobId1 = GenerateJobId("import-1");
        var jobId2 = GenerateJobId("import-2");

        try
        {
            // Create jobs
            await ExecuteImportJobAsync(jobId1);
            await ExecuteImportJobAsync(jobId2);

            // Act
            var jobs = (await Client.GetImportJobsAsync()).ToList();

            // Assert
            Assert.Contains(jobs, j => j.Id == jobId1);
            Assert.Contains(jobs, j => j.Id == jobId2);
            Assert.True(jobs.Count >= 2);

            // All returned jobs should be import jobs
            Assert.All(jobs, job => Assert.Equal("import", job.JobType));

            Output.WriteLine($"✓ Listed {jobs.Count} import jobs");
            foreach (var job in jobs.Take(5)) // Show first 5 for brevity
            {
                Output.WriteLine($"  - Job ID: {job.Id}, Status: {job.Status}");
            }
        }
        finally
        {
            await CleanupImportJobAsync(jobId1);
            await CleanupImportJobAsync(jobId2);
        }
    }

    [Fact]
    public async Task ImportGraphAsync_ShouldThrowException_ForDuplicateJobId()
    {
        // Arrange
        var jobId = GenerateJobId("import");

        try
        {
            await ExecuteImportJobAsync(jobId);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ExecuteImportJobAsync(jobId)
            );

            Assert.Contains("already exists", exception.Message);
            Output.WriteLine($"✓ Duplicate job ID correctly threw exception: {exception.Message}");
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Fact]
    public async Task CancelImportJob_ShouldReturnTrue_WhenJobExists()
    {
        // Arrange
        var jobId = GenerateJobId("import");

        try
        {
            // Create a job first
            await ExecuteImportJobAsync(jobId);

            // Act
            var success = Client.CancelImportJob(jobId);
            var retrievedJob = Client.GetImportJob(jobId);

            // Assert
            Assert.True(success);
            Assert.NotNull(retrievedJob);
            // Note: Status might be Cancelled or another status depending on timing
            Output.WriteLine($"✓ Cancelled job with status: {retrievedJob.Status}");
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Fact]
    public async Task DeleteImportJob_ShouldReturnTrue_WhenJobExists()
    {
        // Arrange
        var jobId = GenerateJobId("import");

        // Create a job first
        await ExecuteImportJobAsync(jobId);

        // Act
        var success = Client.DeleteImportJob(jobId);
        var retrievedJob = Client.GetImportJob(jobId);

        // Assert
        Assert.True(success);
        Assert.Null(retrievedJob);

        Output.WriteLine("✓ Deleted job - retrieval result: null (success)");
    }
}
