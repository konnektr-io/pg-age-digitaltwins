using System.Text;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Infrastructure;

/// <summary>
/// Base class for import job tests providing specialized functionality for import operations.
/// </summary>
public abstract class ImportJobTestBase : JobTestBase
{
    protected ImportJobTestBase(ITestOutputHelper output)
        : base(output) { }

    /// <summary>
    /// Creates a memory stream with test import data.
    /// </summary>
    protected MemoryStream CreateTestImportStream(string? data = null)
    {
        data ??= TestDataFactory.ImportData.CreateValidNdJson();
        var bytes = Encoding.UTF8.GetBytes(data);
        var stream = new MemoryStream(bytes);

        Output.WriteLine($"Created test import stream with {bytes.Length} bytes");
        return stream;
    }

    /// <summary>
    /// Creates default import job options.
    /// </summary>
    protected ImportJobOptions CreateDefaultOptions()
    {
        return new ImportJobOptions
        {
            ContinueOnFailure = true,
            OperationTimeout = TimeSpan.FromSeconds(30),
            LeaveOpen = false, // Fixed: Don't leave streams open when we're disposing them
        };
    }

    /// <summary>
    /// Creates import job options for error testing.
    /// </summary>
    protected ImportJobOptions CreateErrorTestingOptions()
    {
        return new ImportJobOptions
        {
            ContinueOnFailure = true,
            OperationTimeout = TimeSpan.FromSeconds(10),
        };
    }

    /// <summary>
    /// Executes an import job with test data and returns the result.
    /// </summary>
    protected async Task<JobRecord> ExecuteImportJobAsync(
        string? jobId = null,
        string? data = null,
        ImportJobOptions? options = null
    )
    {
        jobId ??= GenerateJobId("test-import");
        options ??= CreateDefaultOptions();

        using var inputStream = CreateTestImportStream(data);
        using var outputStream = new MemoryStream();

        var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);

        // Log output stream for debugging
        outputStream.Position = 0;
        using var reader = new StreamReader(outputStream);
        var logOutput = await reader.ReadToEndAsync();
        if (!string.IsNullOrEmpty(logOutput))
        {
            Output.WriteLine("Import Log Output:");
            Output.WriteLine(logOutput);
        }

        LogJobResult(result);
        return result;
    }

    /// <summary>
    /// Asserts import job results with expected counts.
    /// </summary>
    protected void AssertImportResults(
        JobRecord job,
        int expectedModels = -1,
        int expectedTwins = -1,
        int expectedRelationships = -1
    )
    {
        AssertJobSuccess(job);
        JobAssertions.AssertImportCountsNonNegative(job);

        if (expectedModels >= 0)
            Assert.Equal(expectedModels, job.ModelsCreated);
        if (expectedTwins >= 0)
            Assert.Equal(expectedTwins, job.TwinsCreated);
        if (expectedRelationships >= 0)
            Assert.Equal(expectedRelationships, job.RelationshipsCreated);
    }

    /// <summary>
    /// Cleans up an import job.
    /// </summary>
    protected async Task CleanupImportJobAsync(string jobId)
    {
        await CleanupJobAsync(jobId, "import");
    }
}
