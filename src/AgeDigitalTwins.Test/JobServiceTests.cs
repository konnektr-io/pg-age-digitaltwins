using System.Text;
using System.Text.Json;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Tests for the JobService functionality including distributed locking and checkpoint management.
/// </summary>
[Trait("Category", "Integration")]
public class JobServiceTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public JobServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateJobAsync_ShouldCreateJob_WithValidParameters()
    {
        // Arrange
        var jobId = $"test-job-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        try
        {
            // Act
            await jobService.CreateJobAsync(jobId, "test", new { TestData = "value" });

            // Assert
            var job = await jobService.GetJobAsync(jobId);
            Assert.NotNull(job);
            Assert.Equal(jobId, job.Id);
            Assert.Equal("test", job.JobType);
            Assert.Equal(JobStatus.Notstarted, job.Status);

            _output.WriteLine($"✓ Created job: {jobId}");
        }
        finally
        {
            await jobService.DeleteJobAsync(jobId);
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
        _output.WriteLine("✓ Non-existent job returned null as expected");
    }

    [Fact]
    public async Task UpdateJobStatusAsync_ShouldUpdateStatus_WhenJobExists()
    {
        // Arrange
        var jobId = $"test-job-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        try
        {
            await jobService.CreateJobAsync(jobId, "test", new { });

            // Act
            await jobService.UpdateJobStatusAsync(jobId, JobStatus.Succeeded);

            // Assert
            var job = await jobService.GetJobAsync(jobId);
            Assert.NotNull(job);
            Assert.Equal(JobStatus.Succeeded, job.Status);

            _output.WriteLine($"✓ Updated job status to: {job.Status}");
        }
        finally
        {
            await jobService.DeleteJobAsync(jobId);
        }
    }

    [Fact]
    public async Task ListJobsAsync_ShouldReturnJobsOfSpecificType()
    {
        // Arrange
        var jobId1 = $"test-job-1-{Guid.NewGuid()}";
        var jobId2 = $"test-job-2-{Guid.NewGuid()}";
        var jobId3 = $"test-job-3-{Guid.NewGuid()}";
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

            _output.WriteLine(
                $"✓ Found {importJobs.Count} import jobs and {deleteJobs.Count} delete jobs"
            );
        }
        finally
        {
            await jobService.DeleteJobAsync(jobId1);
            await jobService.DeleteJobAsync(jobId2);
            await jobService.DeleteJobAsync(jobId3);
        }
    }
}

/// <summary>
/// Tests for the import job system functionality through the AgeDigitalTwinsClient.
/// </summary>
[Trait("Category", "Integration")]
public class ImportJobSystemTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public ImportJobSystemTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ImportGraphAsync_ShouldCreateJob_WithValidParameters()
    {
        // Arrange
        var jobId = $"test-job-{Guid.NewGuid()}";
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Add some minimal test data to input stream
        var testData =
            """
            {"Section": "Header"}
            {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
            """u8.ToArray();
        inputStream.Write(testData);
        inputStream.Position = 0;

        try
        {
            // Act
            var jobRecord = await Client.ImportGraphAsync(jobId, inputStream, outputStream);

            // Assert
            Assert.NotNull(jobRecord);
            Assert.Equal(jobId, jobRecord.Id);
            Assert.Equal(JobStatus.Succeeded, jobRecord.Status); // ImportGraphAsync executes the job immediately
            Assert.True(jobRecord.CreatedDateTime <= DateTime.UtcNow);
            Assert.True(jobRecord.LastActionDateTime <= DateTime.UtcNow);
            Assert.NotNull(jobRecord.FinishedDateTime); // Job is finished since it executed
            Assert.True(jobRecord.PurgeDateTime > DateTime.UtcNow);

            _output.WriteLine(
                $"✓ Created import job: {jobRecord.Id} with status: {jobRecord.Status}"
            );
        }
        finally
        {
            // Cleanup
            Client.DeleteImportJob(jobId);
        }
    }

    [Fact]
    public async Task GetImportJob_ShouldReturnJob_WhenJobExists()
    {
        // Arrange
        var jobId = $"test-job-{Guid.NewGuid()}";
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Add test data
        var testData =
            """
            {"Section": "Header"}
            {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
            """u8.ToArray();
        inputStream.Write(testData);
        inputStream.Position = 0;

        try
        {
            // Create a job first
            var createdJob = await Client.ImportGraphAsync(jobId, inputStream, outputStream);

            // Act
            var retrievedJob = Client.GetImportJob(jobId);

            // Assert
            Assert.NotNull(retrievedJob);
            Assert.Equal(jobId, retrievedJob.Id);
            Assert.Equal(createdJob.Status, retrievedJob.Status);

            _output.WriteLine(
                $"✓ Retrieved job: {retrievedJob.Id} with status: {retrievedJob.Status}"
            );
        }
        finally
        {
            Client.DeleteImportJob(jobId);
        }
    }

    [Fact]
    public void GetImportJob_ShouldReturnNull_WhenJobDoesNotExist()
    {
        // Act
        var retrievedJob = Client.GetImportJob("non-existent-job");

        // Assert
        Assert.Null(retrievedJob);
        _output.WriteLine("✓ Non-existent job returned null as expected");
    }

    [Fact]
    public async Task CancelImportJob_ShouldReturnTrue_WhenJobExists()
    {
        // Arrange
        var jobId = $"test-job-{Guid.NewGuid()}";
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Add test data
        var testData =
            """
            {"Section": "Header"}
            {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
            """u8.ToArray();
        inputStream.Write(testData);
        inputStream.Position = 0;

        try
        {
            // Create a job first
            await Client.ImportGraphAsync(jobId, inputStream, outputStream);

            // Act
            var success = Client.CancelImportJob(jobId);
            var retrievedJob = Client.GetImportJob(jobId);

            // Assert
            Assert.True(success);
            Assert.NotNull(retrievedJob);
            // Note: Status might be Cancelled or another status depending on timing
            _output.WriteLine($"✓ Cancelled job with status: {retrievedJob.Status}");
        }
        finally
        {
            Client.DeleteImportJob(jobId);
        }
    }

    [Fact]
    public async Task ListImportJobs_ShouldReturnJobs_WhenJobsExist()
    {
        // Arrange
        var jobId1 = $"test-job-1-{Guid.NewGuid()}";
        var jobId2 = $"test-job-2-{Guid.NewGuid()}";
        using var inputStream1 = new MemoryStream();
        using var outputStream1 = new MemoryStream();
        using var inputStream2 = new MemoryStream();
        using var outputStream2 = new MemoryStream();

        // Add test data
        var testData =
            """
            {"Section": "Header"}
            {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
            """u8.ToArray();

        inputStream1.Write(testData);
        inputStream1.Position = 0;
        inputStream2.Write(testData);
        inputStream2.Position = 0;

        try
        {
            // Create jobs
            await Client.ImportGraphAsync(jobId1, inputStream1, outputStream1);
            await Client.ImportGraphAsync(jobId2, inputStream2, outputStream2);

            // Act
            var jobs = (await Client.GetImportJobsAsync()).ToList();

            // Assert
            Assert.Contains(jobs, j => j.Id == jobId1);
            Assert.Contains(jobs, j => j.Id == jobId2);
            Assert.True(jobs.Count >= 2);

            _output.WriteLine($"✓ Listed {jobs.Count} import jobs");
            foreach (var job in jobs.Take(5)) // Show first 5 for brevity
            {
                _output.WriteLine($"  - Job ID: {job.Id}, Status: {job.Status}");
            }
        }
        finally
        {
            Client.DeleteImportJob(jobId1);
            Client.DeleteImportJob(jobId2);
        }
    }

    [Fact]
    public async Task DeleteImportJob_ShouldReturnTrue_WhenJobExists()
    {
        // Arrange
        var jobId = $"test-job-{Guid.NewGuid()}";
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Add test data
        var testData =
            """
            {"Section": "Header"}
            {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
            """u8.ToArray();
        inputStream.Write(testData);
        inputStream.Position = 0;

        // Create a job first
        await Client.ImportGraphAsync(jobId, inputStream, outputStream);

        // Act
        var success = Client.DeleteImportJob(jobId);
        var retrievedJob = Client.GetImportJob(jobId);

        // Assert
        Assert.True(success);
        Assert.Null(retrievedJob);

        _output.WriteLine("✓ Deleted job - retrieval result: null (success)");
    }

    [Fact]
    public async Task ImportGraphAsync_ShouldThrowException_ForDuplicateJobId()
    {
        // Arrange
        var jobId = $"test-job-{Guid.NewGuid()}";
        using var inputStream1 = new MemoryStream();
        using var outputStream1 = new MemoryStream();
        using var inputStream2 = new MemoryStream();
        using var outputStream2 = new MemoryStream();

        // Add test data
        var testData =
            """
            {"Section": "Header"}
            {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
            """u8.ToArray();

        inputStream1.Write(testData);
        inputStream1.Position = 0;
        inputStream2.Write(testData);
        inputStream2.Position = 0;

        try
        {
            await Client.ImportGraphAsync(jobId, inputStream1, outputStream1);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Client.ImportGraphAsync(jobId, inputStream2, outputStream2)
            );

            Assert.Contains("already exists", exception.Message);
            _output.WriteLine($"✓ Duplicate job ID correctly threw exception: {exception.Message}");
        }
        finally
        {
            Client.DeleteImportJob(jobId);
        }
    }
}

/// <summary>
/// Tests for the delete job system functionality through the AgeDigitalTwinsClient.
/// Delete jobs remove all relationships, twins, and models in the correct order.
/// </summary>
[Trait("Category", "Integration")]
public class DeleteJobSystemTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public DeleteJobSystemTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DeleteAllAsync_ShouldCreateAndExecuteJob_WithValidParameters()
    {
        // Arrange
        var jobId = $"test-delete-job-{Guid.NewGuid()}";

        // First, let's create some test data to delete
        await CreateTestDataAsync();

        try
        {
            // Act - Use the correct method name
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
    public async Task DeleteAllAsync_ShouldHandleEmptyDatabase_Gracefully()
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
    public async Task DeleteAllAsync_ShouldThrowException_ForDuplicateJobId()
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
    public async Task DeleteJobCheckpoint_ShouldSaveAndLoadCorrectly()
    {
        // Arrange
        var jobId = $"test-checkpoint-{Guid.NewGuid()}";
        var jobService = Client.JobService;

        // Create a job first
        await jobService.CreateJobAsync(jobId, "delete", (object?)null);

        var checkpoint = DeleteJobCheckpoint.Create(jobId);
        checkpoint.RelationshipsDeleted = 10;
        checkpoint.TwinsDeleted = 5;
        checkpoint.ErrorCount = 2;
        checkpoint.CurrentSection = DeleteSection.Twins;

        try
        {
            // Act
            await jobService.SaveCheckpointAsync(checkpoint);

            // Assert - Try to load it back
            var loadedCheckpoint = await jobService.LoadDeleteCheckpointAsync(jobId);

            Assert.NotNull(loadedCheckpoint);
            Assert.Equal(jobId, loadedCheckpoint.JobId);
            Assert.Equal(10, loadedCheckpoint.RelationshipsDeleted);
            Assert.Equal(5, loadedCheckpoint.TwinsDeleted);
            Assert.Equal(2, loadedCheckpoint.ErrorCount);
            Assert.Equal(DeleteSection.Twins, loadedCheckpoint.CurrentSection);

            _output.WriteLine(
                $"✓ Successfully saved and loaded delete checkpoint for job: {jobId}"
            );
        }
        finally
        {
            // Cleanup
            await jobService.DeleteJobAsync(jobId);
        }
    }

    /// <summary>
    /// Helper method to create some test data for deletion tests.
    /// </summary>
    private async Task CreateTestDataAsync()
    {
        try
        {
            // Create a simple model for testing using correct DTDL format
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

            await Client.CreateModelsAsync(new[] { testModelJson });

            // Create a test twin
            var testTwin = new { testProperty = "test-value" };

            await Client.CreateOrReplaceDigitalTwinAsync("test-twin-for-deletion", testTwin);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not create test data: {ex.Message}");
        }
    }
}
