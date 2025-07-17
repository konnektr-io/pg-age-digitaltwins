using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class BatchDigitalTwinTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public BatchDigitalTwinTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinsAsync_WithValidTwins_ShouldSucceed()
    {
        // Arrange - Load required models using existing sample data
        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        await Client.CreateModelsAsync(models);

        // Create twins using sample data patterns
        var digitalTwins = new List<string>
        {
            SampleData.TwinRoom1,
            SampleData.TwinTemperatureSensor1,
        };

        // Act
        var result = await Client.CreateOrReplaceDigitalTwinsAsync(digitalTwins);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.False(result.HasFailures);
        Assert.Equal(2, result.Results.Count);

        foreach (var operationResult in result.Results)
        {
            Assert.True(operationResult.IsSuccess);
            Assert.Null(operationResult.ErrorMessage);
            Assert.Contains(operationResult.DigitalTwinId, new[] { "room1", "sensor1" });
        }

        _output.WriteLine(
            $"Batch operation completed successfully: {result.SuccessCount} successes, {result.FailureCount} failures"
        );
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinsAsync_WithInvalidTwins_ShouldReportFailures()
    {
        // Arrange - Load required models
        string[] models = [SampleData.DtdlRoom];
        await Client.CreateModelsAsync(models);

        var digitalTwins = new List<string>
        {
            SampleData.TwinRoom1, // Valid twin
            @"{""$dtId"": ""invalidTwin"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""temperature"": ""invalid_string""}", // Invalid property
            @"{""$dtId"": ""missingModelTwin"", ""$metadata"": {""$model"": ""dtmi:com:example:NonExistentModel;1""}, ""temperature"": 20.0}", // Non-existent model
        };

        // Act
        var result = await Client.CreateOrReplaceDigitalTwinsAsync(digitalTwins);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.SuccessCount); // Only room1 should succeed
        Assert.Equal(2, result.FailureCount); // invalidTwin and missingModelTwin should fail
        Assert.True(result.HasFailures);

        var successfulResults = result.Results.Where(r => r.IsSuccess).ToList();
        var failedResults = result.Results.Where(r => !r.IsSuccess).ToList();

        Assert.Single(successfulResults);
        Assert.Equal("room1", successfulResults[0].DigitalTwinId);

        Assert.Equal(2, failedResults.Count);
        Assert.Contains(failedResults, r => r.DigitalTwinId == "invalidTwin");
        Assert.Contains(failedResults, r => r.DigitalTwinId == "missingModelTwin");

        foreach (var failedResult in failedResults)
        {
            Assert.False(failedResult.IsSuccess);
            Assert.NotNull(failedResult.ErrorMessage);
            Assert.NotEmpty(failedResult.ErrorMessage);
        }

        _output.WriteLine(
            $"Batch operation completed with mixed results: {result.SuccessCount} successes, {result.FailureCount} failures"
        );
        foreach (var failedResult in failedResults)
        {
            _output.WriteLine(
                $"Failed twin '{failedResult.DigitalTwinId}': {failedResult.ErrorMessage}"
            );
        }
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinsAsync_WithEmptyBatch_ShouldReturnEmptyResult()
    {
        // Arrange
        var digitalTwins = new List<string>();

        // Act
        var result = await Client.CreateOrReplaceDigitalTwinsAsync(digitalTwins);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.False(result.HasFailures);
        Assert.Empty(result.Results);

        _output.WriteLine("Empty batch operation completed successfully");
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinsAsync_WithOversizedBatch_ShouldThrowException()
    {
        // Arrange
        var digitalTwins = new List<string>();
        for (int i = 0; i < 101; i++) // Exceed the limit of 100
        {
            digitalTwins.Add(
                $@"{{""$dtId"": ""twin{i}"", ""$metadata"": {{""$model"": ""dtmi:com:adt:dtsample:room;1""}}, ""temperature"": 25.0}}"
            );
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => Client.CreateOrReplaceDigitalTwinsAsync(digitalTwins)
        );

        Assert.Contains("Batch size (101) exceeds maximum allowed size (100)", exception.Message);
        _output.WriteLine($"Expected exception thrown: {exception.Message}");
    }
}
