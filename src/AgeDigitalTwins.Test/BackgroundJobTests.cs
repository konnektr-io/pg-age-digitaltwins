using System.Text;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class BackgroundJobTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public BackgroundJobTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ImportGraphAsync_WithBackgroundExecution_ShouldReturnImmediately()
    {
        // Arrange
        var jobId = "test-bg-job-" + Guid.NewGuid().ToString("N")[..8];
        var testData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""test""}
{""Section"": ""Models""}
{""@id"":""dtmi:example:Model;1"",""@type"":""Interface"",""@context"":""dtmi:dtdl:context;2""}
";

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

        _output.WriteLine(
            $"Background job {result.Id} returned in {duration.TotalMilliseconds}ms with status: {result.Status}"
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

        _output.WriteLine(
            $"Background job {finalResult.Id} completed with status: {finalResult.Status}"
        );
    }

    [Fact]
    public async Task ImportGraphAsync_WithSynchronousExecution_ShouldWaitForCompletion()
    {
        // Arrange
        var jobId = "test-sync-job-" + Guid.NewGuid().ToString("N")[..8];
        var testData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""test""}
{""Section"": ""Models""}
{""@id"":""dtmi:example:Model;1"",""@type"":""Interface"",""@context"":""dtmi:dtdl:context;2""}
";

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

        _output.WriteLine(
            $"Synchronous job {result.Id} completed in {duration.TotalMilliseconds}ms with status: {result.Status}"
        );
    }
}
