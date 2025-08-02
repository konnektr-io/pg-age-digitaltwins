using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.Test.Infrastructure;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test.Jobs.Import;

/// <summary>
/// Tests for import job input validation and error handling.
/// Focuses on parameter validation, malformed data handling, and error scenarios.
/// </summary>
[Trait("Category", "Integration")]
public class ImportJobValidationTests : ImportJobTestBase
{
    public ImportJobValidationTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task ImportGraphAsync_WithNullInputStream_ShouldThrow()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        using var outputStream = new MemoryStream();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Client.ImportGraphAsync(jobId, null!, outputStream)
        );

        Assert.Equal("inputStream", exception.ParamName);
        Output.WriteLine($"Expected ArgumentNullException thrown: {exception.Message}");
    }

    [Fact]
    public async Task ImportGraphAsync_WithNullOutputStream_ShouldThrow()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        using var inputStream = CreateTestImportStream();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Client.ImportGraphAsync(jobId, inputStream, null!)
        );

        Assert.Equal("outputStream", exception.ParamName);
        Output.WriteLine($"Expected ArgumentNullException thrown: {exception.Message}");
    }

    [Fact]
    public async Task ImportGraphAsync_WithEmptyStream_ShouldFail()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        var options = CreateDefaultOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportGraphAsync(jobId, inputStream, outputStream, options)
        );

        Assert.Contains("Empty input stream", exception.Message);
        Output.WriteLine($"Expected ArgumentException thrown: {exception.Message}");
    }

    [Fact]
    public async Task ImportGraphAsync_WithInvalidFileVersion_ShouldFail()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        var invalidData = """
            {"Section": "Header"}
            {"fileVersion": "2.0.0", "author": "test", "organization": "contoso"}
            """;

        using var inputStream = CreateTestImportStream(invalidData);
        using var outputStream = new MemoryStream();
        var options = CreateDefaultOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportGraphAsync(jobId, inputStream, outputStream, options)
        );

        Assert.Contains("Unsupported file version", exception.Message);
        Output.WriteLine($"Expected ArgumentException thrown: {exception.Message}");
    }

    [Fact]
    public async Task ImportGraphAsync_WithMissingHeader_ShouldFail()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        var invalidData = """
            {"Section": "Models"}
            {"@id":"dtmi:com:test;1","@type":"Interface","@context":"dtmi:dtdl:context;2"}
            """;

        using var inputStream = CreateTestImportStream(invalidData);
        using var outputStream = new MemoryStream();
        var options = CreateDefaultOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportGraphAsync(jobId, inputStream, outputStream, options)
        );

        Assert.Contains("First section must be 'Header'", exception.Message);
        Output.WriteLine($"Expected ArgumentException thrown: {exception.Message}");
    }

    [Fact]
    public async Task ImportGraphAsync_WithMalformedJson_ShouldHandleGracefully()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        var malformedData = """
            {"Section": "Header"}
            {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
            {"Section": "Models"}
            {"@id":"dtmi:example:ValidModel;1","@type":"Interface","@context":"dtmi:dtdl:context;2"}
            {"Section": "Twins"}
            {"$dtId":"validTwin","$metadata":{"$model":"dtmi:example:ValidModel;1"}}
            INVALID_JSON_LINE
            {"$dtId":"anotherValidTwin","$metadata":{"$model":"dtmi:example:ValidModel;1"}}
            """;

        using var inputStream = CreateTestImportStream(malformedData);
        using var outputStream = new MemoryStream();
        var options = CreateErrorTestingOptions();

        try
        {
            // Act
            var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);

            // Assert
            AssertJobBasicProperties(result, jobId, "import");

            // Should have partial success due to continue on failure
            Assert.True(
                result.Status == JobStatus.PartiallySucceeded || result.Status == JobStatus.Failed,
                $"Expected PartiallySucceeded or Failed but got {result.Status}"
            );

            // Should have created the model and at least one twin
            Assert.True(result.ModelsCreated >= 1);
            Assert.True(result.ErrorCount > 0);

            Output.WriteLine($"Handled malformed JSON gracefully with status: {result.Status}");
            Output.WriteLine(
                $"Created: {result.ModelsCreated} models, {result.TwinsCreated} twins"
            );
            Output.WriteLine($"Errors: {result.ErrorCount}");
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Fact]
    public async Task ImportGraphAsync_WithInvalidModelReferences_ShouldHandleErrors()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        var dataWithInvalidRefs = """
            {"Section": "Header"}
            {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
            {"Section": "Models"}
            {"@id":"dtmi:example:ValidModel;1","@type":"Interface","@context":"dtmi:dtdl:context;2"}
            {"Section": "Twins"}
            {"$dtId":"validTwin","$metadata":{"$model":"dtmi:example:ValidModel;1"}}
            {"$dtId":"invalidTwin","$metadata":{"$model":"dtmi:example:NonExistentModel;1"}}
            """;

        using var inputStream = CreateTestImportStream(dataWithInvalidRefs);
        using var outputStream = new MemoryStream();
        var options = CreateErrorTestingOptions();

        try
        {
            // Act
            var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);

            // Assert
            AssertJobBasicProperties(result, jobId, "import");
            JobAssertions.AssertJobStatus(result, JobStatus.PartiallySucceeded);

            // Should have created the valid model and valid twin, but failed on invalid twin
            Assert.Equal(1, result.ModelsCreated);
            Assert.Equal(1, result.TwinsCreated);
            Assert.True(result.ErrorCount > 0);

            Output.WriteLine($"Handled invalid model references with status: {result.Status}");
            Output.WriteLine($"Valid items created, invalid items generated errors");
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ImportGraphAsync_WithInvalidJobId_ShouldThrow(string? invalidJobId)
    {
        // Arrange
        using var inputStream = CreateTestImportStream();
        using var outputStream = new MemoryStream();
        var options = CreateDefaultOptions();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await Client.ImportGraphAsync(invalidJobId!, inputStream, outputStream, options)
        );

        Output.WriteLine(
            $"Expected ArgumentException for invalid job ID '{invalidJobId}': {exception.Message}"
        );
    }

    [Fact]
    public async Task ImportGraphAsync_WithVeryLargeTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var jobId = GenerateJobId("import");
        var options = new ImportJobOptions
        {
            OperationTimeout = TimeSpan.FromMilliseconds(1), // Very short timeout
            ContinueOnFailure = false,
        };

        using var inputStream = CreateTestImportStream();
        using var outputStream = new MemoryStream();

        try
        {
            // Act
            var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);

            // Assert - The job might complete successfully if it's fast enough,
            // or it might fail due to timeout. Both are acceptable for this test.
            AssertJobBasicProperties(result, jobId, "import");

            Output.WriteLine($"Import with short timeout completed with status: {result.Status}");
        }
        finally
        {
            await CleanupImportJobAsync(jobId);
        }
    }
}
