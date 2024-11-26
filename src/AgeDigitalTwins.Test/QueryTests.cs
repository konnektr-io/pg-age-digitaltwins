using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using Azure.DigitalTwins.Core;
using DTDLParser;
using Json.Patch;
using Json.Pointer;

namespace AgeDigitalTwins.Test;

public class QueryTests : TestBase
{
    public QueryTests()
    {
        // Load required models
        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        Client.CreateModelsAsync(models).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task QueryAsync_SimpleQuery_ReturnsTwinsAndRelationships()
    {
        var roomTwin = @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}";
        await Client.CreateOrReplaceDigitalTwinAsync("room1", roomTwin);
        var sensorTwin = @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}";
        await Client.CreateOrReplaceDigitalTwinAsync("sensor1", sensorTwin);
        var relationship = @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}";
        var returnRel = await Client.CreateOrReplaceRelationshipAsync("room1", "rel1", relationship);
        Assert.NotNull(returnRel);

        await foreach (var line in Client.QueryAsync<JsonDocument>(@"
        MATCH (r:Twin { `$dtId`: 'room1' })-[rel:rel_has_sensors]->(s:Twin)
        RETURN r, rel, s
        "))
        {
            Assert.NotNull(line);
            Assert.Equal("room1", line.RootElement.GetProperty("r").GetProperty("$dtId").GetString());
            Assert.Equal("rel1", line.RootElement.GetProperty("rel").GetProperty("$relationshipId").GetString());
            Assert.Equal("sensor1", line.RootElement.GetProperty("rel").GetProperty("$targetId").GetString());
            Assert.Equal("sensor1", line.RootElement.GetProperty("s").GetProperty("$dtId").GetString());
        }
    }

    [Fact]
    public async Task QueryAsync_RelationshipsQuery_ReturnsRelationship()
    {
        var roomTwin = @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}";
        await Client.CreateOrReplaceDigitalTwinAsync("room1", roomTwin);
        var sensorTwin = @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}";
        await Client.CreateOrReplaceDigitalTwinAsync("sensor1", sensorTwin);
        var relationship = @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}";
        var returnRel = await Client.CreateOrReplaceRelationshipAsync("room1", "rel1", relationship);
        Assert.NotNull(returnRel);

        await foreach (var line in Client.QueryAsync<JsonDocument>(@"
        MATCH (r:Twin)-[rel:rel_has_sensors]->(s:Twin)
        WHERE rel['$relationshipId'] = 'rel1'
        RETURN rel
        "))
        {
            Assert.NotNull(line);
            Assert.Equal("rel1", line.RootElement.GetProperty("rel").GetProperty("$relationshipId").GetString());
            Assert.Equal("sensor1", line.RootElement.GetProperty("rel").GetProperty("$targetId").GetString());
        }
    }

    [Fact]
    public async Task QueryAsync_SimpleAdtQuery_ReturnsTwins()
    {
        Dictionary<string, string> twins = new()
        {
            {"room1", @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"},
            {"room2", @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"}
        };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (var line in Client.QueryAsync<JsonDocument>(@"
        SELECT T FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "))
        {
            Assert.NotNull(line);
            var id = line.RootElement.GetProperty("T").GetProperty("$dtId").GetString();
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_SimpleAdtQueryWithUnderscore_ReturnsTwins()
    {
        Dictionary<string, string> twins = new()
        {
            {"room1", @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"},
            {"room2", @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"}
        };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (var line in Client.QueryAsync<JsonDocument>(@"
        SELECT _ FROM DIGITALTWINS _ WHERE _.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "))
        {
            Assert.NotNull(line);
            var id = line.RootElement.GetProperty("_").GetProperty("$dtId").GetString();
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_SimpleAdtQuerySelectProperty_ReturnsPropertyValues()
    {
        Dictionary<string, string> twins = new()
        {
            {"room1", @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"},
            {"room2", @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"}
        };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (var line in Client.QueryAsync<JsonDocument>(@"
        SELECT T.name FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "))
        {
            Assert.NotNull(line);
            var name = line.RootElement.GetProperty("name").GetString();
            Assert.StartsWith("Room", name);
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_SimpleAdtQuerySelectAlias_ReturnsPropertyValues()
    {
        Dictionary<string, string> twins = new()
        {
            {"room1", @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"},
            {"room2", @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"}
        };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (var line in Client.QueryAsync<JsonDocument>(@"
        SELECT T.name AS name FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "))
        {
            Assert.NotNull(line);
            var name = line.RootElement.GetProperty("name").GetString();
            Assert.StartsWith("Room", name);
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_AdtQueryWithTop_ReturnsTwins()
    {
        Dictionary<string, string> twins = new()
        {
            {"room1", @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"},
            {"room2", @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"},
            {"room3", @"{""$dtId"": ""room3"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 3""}"}
        };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (var line in Client.QueryAsync<JsonDocument>(@"
        SELECT TOP(1) T FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "))
        {
            Assert.NotNull(line);
            var id = line.RootElement.GetProperty("T").GetProperty("$dtId").GetString();
            count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task QueryAsync_AdtQueryWithCount_ReturnsCount()
    {
        Dictionary<string, string> twins = new()
        {
            {"room1", @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""notveryunique""}"},
            {"room2", @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""notveryunique""}"},
            {"room3", @"{""$dtId"": ""room3"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""notveryunique""}"}
        };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (var line in Client.QueryAsync<JsonDocument>(@"SELECT COUNT() FROM DIGITALTWINS T WHERE T.name = 'notveryunique'"))
        {
            Assert.NotNull(line);
            Assert.Equal(3, line.RootElement.GetProperty("COUNT").GetInt16());
            count++;
        }
        Assert.Equal(1, count);
    }
}
