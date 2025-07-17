using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgeDigitalTwins.Models;
using Xunit;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class BatchRelationshipTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public BatchRelationshipTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipsAsync_WithValidRelationships_ShouldSucceed()
    {
        // Arrange - Load required models and create twins using existing sample data
        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        await Client.CreateModelsAsync(models);

        // Create twins using sample data
        await Client.CreateOrReplaceDigitalTwinAsync("room1", SampleData.TwinRoom1);
        await Client.CreateOrReplaceDigitalTwinAsync("sensor1", SampleData.TwinTemperatureSensor1);

        // Create a second room and sensor for batch testing
        var room2 = SampleData.TwinRoom1.Replace("room1", "room2").Replace("Room 1", "Room 2");
        var sensor2 = SampleData
            .TwinTemperatureSensor1.Replace("sensor1", "sensor2")
            .Replace("Temperature Sensor 1", "Temperature Sensor 2");

        await Client.CreateOrReplaceDigitalTwinAsync("room2", room2);
        await Client.CreateOrReplaceDigitalTwinAsync("sensor2", sensor2);

        // Create relationships using the existing relationship pattern
        var relationships = new List<string>
        {
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}",
            @"{""$relationshipId"": ""rel2"", ""$sourceId"": ""room2"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor2""}",
        };

        // Act
        var result = await Client.CreateOrReplaceRelationshipsAsync(relationships);

        var readRel1 = await Client.GetRelationshipAsync<JsonDocument>("room1", "rel1");
        var readRel2 = await Client.GetRelationshipAsync<JsonDocument>("room2", "rel2");

        // Assert
        Assert.NotNull(readRel1);
        Assert.Equal("sensor1", readRel1.RootElement.GetProperty("$targetId").GetString());

        Assert.NotNull(readRel2);
        Assert.Equal("sensor2", readRel2.RootElement.GetProperty("$targetId").GetString());

        Assert.NotNull(result);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.False(result.HasFailures);
        Assert.Equal(2, result.Results.Count);

        foreach (var operationResult in result.Results)
        {
            Assert.True(operationResult.IsSuccess);
            Assert.Null(operationResult.ErrorMessage);
            Assert.Contains(operationResult.RelationshipId, new[] { "rel1", "rel2" });
        }

        _output.WriteLine(
            $"Batch relationship operation completed successfully: {result.SuccessCount} successes, {result.FailureCount} failures"
        );
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipsAsync_WithInvalidRelationships_ShouldReportFailures()
    {
        // Arrange - Load required models and create only one twin
        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        await Client.CreateModelsAsync(models);

        // Create only one twin
        await Client.CreateOrReplaceDigitalTwinAsync("room1", SampleData.TwinRoom1);

        // Create relationships - one with non-existent target, one with non-existent source
        var relationships = new List<string>
        {
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""nonExistentSensor""}",
            @"{""$relationshipId"": ""rel2"", ""$sourceId"": ""nonExistentRoom"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""room1""}",
        };

        // Act
        var result = await Client.CreateOrReplaceRelationshipsAsync(relationships);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.True(result.HasFailures);

        var failedResults = result.Results.Where(r => !r.IsSuccess).ToList();
        Assert.Equal(2, failedResults.Count);

        foreach (var failedResult in failedResults)
        {
            Assert.False(failedResult.IsSuccess);
            Assert.NotNull(failedResult.ErrorMessage);
            Assert.Contains("does not exist", failedResult.ErrorMessage);
        }

        _output.WriteLine(
            $"Batch relationship operation completed with expected failures: {result.SuccessCount} successes, {result.FailureCount} failures"
        );
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipsAsync_WithEmptyBatch_ShouldReturnEmptyResult()
    {
        // Arrange
        var relationships = new List<string>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<System.ArgumentException>(
            () => Client.CreateOrReplaceRelationshipsAsync(relationships)
        );

        Assert.Contains("Relationships cannot be empty", exception.Message);
        _output.WriteLine("Empty batch operation threw expected exception");
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipsAsync_WithOversizedBatch_ShouldThrowException()
    {
        // Arrange
        var relationships = new List<string>();
        for (int i = 0; i < 101; i++) // Exceed the limit of 100
        {
            relationships.Add(
                $@"{{""$relationshipId"": ""rel{i}"", ""$sourceId"": ""source{i}"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""target{i}""}}"
            );
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<System.ArgumentException>(
            () => Client.CreateOrReplaceRelationshipsAsync(relationships)
        );

        Assert.Contains("Cannot process more than 100 relationships", exception.Message);
        _output.WriteLine($"Expected exception thrown: {exception.Message}");
    }
}
