using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using Azure.DigitalTwins.Core;
using DTDLParser;
using Json.Patch;
using Json.Pointer;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
[Collection("Sequential Integration Tests")]
public class RelationshipTests : TestBase
{
    [Fact]
    public async Task CreateOrReplaceRelationshipAsync_BasicRelationship_CreatedAndReadable()
    {
        // Load required models
        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        await Client.CreateModelsAsync(models);

        var roomTwin =
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}";
        await Client.CreateOrReplaceDigitalTwinAsync("room1", roomTwin);
        var sensorTwin =
            @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}";
        await Client.CreateOrReplaceDigitalTwinAsync("sensor1", sensorTwin);
        var relationship =
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}";
        var returnRel = await Client.CreateOrReplaceRelationshipAsync(
            "room1",
            "rel1",
            relationship
        );
        Assert.NotNull(returnRel);

        var readRelationship = await Client.GetRelationshipAsync<JsonDocument>("room1", "rel1");
        Assert.NotNull(readRelationship);
        var relElement = readRelationship.RootElement;
        Assert.Equal("rel1", relElement.GetProperty("$relationshipId").GetString());
        Assert.Equal("sensor1", relElement.GetProperty("$targetId").GetString());
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipAsync_BasicRelationshipNoSourceOrId_CreatedAndReadable()
    {
        // Load required models
        try
        {
            string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
            await Client.CreateModelsAsync(models);
        }
        catch { }

        var roomTwin =
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}";
        await Client.CreateOrReplaceDigitalTwinAsync("room1", roomTwin);
        var sensorTwin =
            @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}";
        await Client.CreateOrReplaceDigitalTwinAsync("sensor1", sensorTwin);
        var relationship =
            @"{""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}";
        var returnRel = await Client.CreateOrReplaceRelationshipAsync(
            "room1",
            "rel1",
            relationship
        );
        Assert.NotNull(returnRel);

        var readRelationship = await Client.GetRelationshipAsync<JsonDocument>("room1", "rel1");
        Assert.NotNull(readRelationship);
        var relElement = readRelationship.RootElement;
        Assert.Equal("rel1", relElement.GetProperty("$relationshipId").GetString());
        Assert.Equal("sensor1", relElement.GetProperty("$targetId").GetString());
        Assert.Equal("room1", relElement.GetProperty("$sourceId").GetString());
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipAsync_WithIfNoneMatch_ThrowsOnExistingRelationship()
    {
        // Load required models
        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        await Client.CreateModelsAsync(models);

        var roomTwin =
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}";
        await Client.CreateOrReplaceDigitalTwinAsync("room1", roomTwin);
        var sensorTwin =
            @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}";
        await Client.CreateOrReplaceDigitalTwinAsync("sensor1", sensorTwin);
        var relationship =
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}";
        await Client.CreateOrReplaceRelationshipAsync("room1", "rel1", relationship);

        var relationship2 =
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}";
        await Assert.ThrowsAsync<PreconditionFailedException>(
            () => Client.CreateOrReplaceRelationshipAsync("room1", "rel1", relationship2, "*")
        );
    }
}
