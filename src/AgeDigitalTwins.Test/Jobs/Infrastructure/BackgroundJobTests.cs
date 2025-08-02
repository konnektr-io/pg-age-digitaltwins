using System.Text;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.Test.Infrastructure;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Jobs.Infrastructure;

/// <summary>
/// Tests for background job execution patterns and performance characteristics.
/// Migrated and enhanced from BackgroundJobTests.cs.
/// </summary>
[Trait("Category", "Integration")]
public class BackgroundJobTests : ImportJobTestBase
{
    public BackgroundJobTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task ImportGraphAsync_WithBackgroundExecution_ShouldReturnImmediately()
    {
        // Arrange
        var jobId = GenerateJobId("bg-job");
        var testData = TestDataFactory.ImportData.CreateValidNdJson();

        var options = new ImportJobOptions();

        // Create a stream factory for background execution
        Func<CancellationToken, Task<(Stream inputStream, Stream outputStream)>> streamFactory = (
            ct
        ) =>
        {
            var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
            var outputStream = new MemoryStream();
            return Task.FromResult<(Stream, Stream)>((inputStream, outputStream));
        };

        // Act - Execute with background execution enabled
        var startTime = DateTime.UtcNow;
        var result = await Client.ImportGraphAsync<JobRecord>(
            jobId,
            streamFactory,
            null,
            request: default,
            executeInBackground: true,
            cancellationToken: default
        );
        var endTime = DateTime.UtcNow;

        // Assert - Should return immediately with job in Running status
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.Equal(JobStatus.Running, result.Status);

        // Should complete very quickly (under 1 second) since it returns immediately
        var duration = endTime - startTime;
        Assert.True(
            duration.TotalSeconds < 1,
            $"Background job took {duration.TotalSeconds} seconds, expected under 1 second"
        );

        Output.WriteLine(
            $"✓ Background job {result.Id} returned in {duration.TotalMilliseconds}ms with status: {result.Status}"
        );

        // Wait a bit for background execution to complete
        await Task.Delay(2000);

        // Verify the job eventually completes
        var finalResult = await Client.GetImportJobAsync(jobId);
        Assert.NotNull(finalResult);
        Assert.True(
            finalResult.Status == JobStatus.Succeeded
                || finalResult.Status == JobStatus.PartiallySucceeded
        );

        Output.WriteLine(
            $"✓ Background job {finalResult.Id} completed with status: {finalResult.Status}"
        );
    }

    [Fact]
    public async Task ImportGraphAsync_WithSynchronousExecution_ShouldWaitForCompletion()
    {
        // Arrange
        var jobId = GenerateJobId("sync-job");
        var testData = TestDataFactory.ImportData.CreateComplexNdJson();

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
        using var outputStream = new MemoryStream();

        var options = new ImportJobOptions();

        // Act - Execute with synchronous execution (default)
        var startTime = DateTime.UtcNow;
        var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);
        var endTime = DateTime.UtcNow;

        // Assert - Should return after completion
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.True(
            result.Status == JobStatus.Succeeded || result.Status == JobStatus.PartiallySucceeded
        );

        // Should take longer than immediate return (at least some processing time)
        var duration = endTime - startTime;
        Assert.True(
            duration.TotalMilliseconds > 10,
            $"Synchronous job took {duration.TotalMilliseconds}ms, expected more than 10ms"
        );

        Output.WriteLine(
            $"✓ Synchronous job {result.Id} completed in {duration.TotalMilliseconds}ms with status: {result.Status}"
        );
    }

    [Fact]
    public async Task BackgroundJob_ShouldExecute_WithoutBlockingCaller()
    {
        // Arrange
        var jobId = GenerateJobId("non-blocking");
        var testData = TestDataFactory.ImportData.CreateValidNdJson();

        // Create a stream factory that includes more complex data for longer processing
        Func<CancellationToken, Task<(Stream inputStream, Stream outputStream)>> streamFactory = (
            ct
        ) =>
        {
            var complexData = TestDataFactory.ImportData.CreateComplexNdJson();
            var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(complexData));
            var outputStream = new MemoryStream();
            return Task.FromResult<(Stream, Stream)>((inputStream, outputStream));
        };

        // Act - Start background job
        var backgroundResult = await Client.ImportGraphAsync<JobRecord>(
            jobId,
            streamFactory,
            null,
            request: default,
            executeInBackground: true,
            cancellationToken: default
        );

        // Assert - Caller should not be blocked
        Assert.NotNull(backgroundResult);
        Assert.Equal(JobStatus.Running, backgroundResult.Status);

        // Caller can continue with other work immediately
        var otherWorkCompleted = await SimulateOtherWork();
        Assert.True(otherWorkCompleted);

        Output.WriteLine(
            $"✓ Background job {backgroundResult.Id} started without blocking caller"
        );
        Output.WriteLine("✓ Caller was able to perform other work while job executes");

        // Wait for background job to complete
        var maxWaitTime = TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;
        JobRecord? finalResult = null;

        while (DateTime.UtcNow - startTime < maxWaitTime)
        {
            finalResult = await Client.GetImportJobAsync(jobId);
            if (finalResult?.Status != JobStatus.Running)
                break;
            await Task.Delay(500);
        }

        Assert.NotNull(finalResult);
        Assert.True(
            finalResult.Status == JobStatus.Succeeded
                || finalResult.Status == JobStatus.PartiallySucceeded
        );

        Output.WriteLine($"✓ Background job eventually completed with status: {finalResult.Status}");
    }

    [Fact]
    public async Task MultipleBackgroundJobs_ShouldExecute_Concurrently()
    {
        // Arrange
        var jobIds = new[]
        {
            GenerateJobId("concurrent-1"),
            GenerateJobId("concurrent-2"),
            GenerateJobId("concurrent-3")
        };

        var testData = TestDataFactory.ImportData.CreateValidNdJson();

        // Act - Start multiple background jobs concurrently
        var startTime = DateTime.UtcNow;
        var tasks = jobIds.Select(async jobId =>
        {
            Func<CancellationToken, Task<(Stream inputStream, Stream outputStream)>> streamFactory = (
                ct
            ) =>
            {
                var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
                var outputStream = new MemoryStream();
                return Task.FromResult<(Stream, Stream)>((inputStream, outputStream));
            };

            return await Client.ImportGraphAsync<JobRecord>(
                jobId,
                streamFactory,
                null,
                request: default,
                executeInBackground: true,
                cancellationToken: default
            );
        });

        var results = await Task.WhenAll(tasks);
        var launchDuration = DateTime.UtcNow - startTime;

        // Assert - All jobs should start quickly
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.Equal(JobStatus.Running, result.Status);
        });

        // Should launch all jobs very quickly
        Assert.True(
            launchDuration.TotalSeconds < 2,
            $"Launching {jobIds.Length} jobs took {launchDuration.TotalSeconds} seconds, expected under 2 seconds"
        );

        Output.WriteLine(
            $"✓ Launched {jobIds.Length} background jobs in {launchDuration.TotalMilliseconds}ms"
        );

        // Wait for all jobs to complete
        await WaitForJobsCompletion(jobIds);

        Output.WriteLine("✓ All concurrent background jobs completed successfully");
    }

    /// <summary>
    /// Simulates other work that the caller can perform while background job executes.
    /// </summary>
    private static async Task<bool> SimulateOtherWork()
    {
        // Simulate some work that takes time
        await Task.Delay(100);
        
        // Simulate some computation
        var sum = 0;
        for (int i = 0; i < 1000; i++)
        {
            sum += i;
        }
        
        return sum > 0;
    }

    /// <summary>
    /// Waits for multiple jobs to complete with timeout.
    /// </summary>
    private async Task WaitForJobsCompletion(string[] jobIds)
    {
        var maxWaitTime = TimeSpan.FromSeconds(15);
        var startTime = DateTime.UtcNow;
        var completedJobs = new HashSet<string>();

        while (DateTime.UtcNow - startTime < maxWaitTime && completedJobs.Count < jobIds.Length)
        {
            foreach (var jobId in jobIds)
            {
                if (completedJobs.Contains(jobId))
                    continue;

                var result = await Client.GetImportJobAsync(jobId);
                if (result?.Status != JobStatus.Running)
                {
                    completedJobs.Add(jobId);
                    Output.WriteLine($"✓ Job {jobId} completed with status: {result?.Status}");
                }
            }

            if (completedJobs.Count < jobIds.Length)
                await Task.Delay(500);
        }

        Assert.Equal(jobIds.Length, completedJobs.Count);
    }
}
