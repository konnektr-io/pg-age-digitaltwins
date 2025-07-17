using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Tests for the JobService functionality.
/// </summary>
[Trait("Category", "Integration")]
public class JobServiceTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public JobServiceTests(ITestOutputHelper output)
    {
        _output = output;
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
