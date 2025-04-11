using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using Azure.DigitalTwins.Core;
using DTDLParser;
using Json.Patch;
using Json.Pointer;

namespace AgeDigitalTwins.Test;

public class QueryTests : TestBase
{
    internal async Task IntializeAsync()
    {
        try
        {
            // Load required models
            string[] models =
            [
                SampleData.DtdlRoom,
                SampleData.DtdlTemperatureSensor,
                SampleData.DtdlPlanet,
                SampleData.DtdlCelestialBody,
                SampleData.DtdlCrater,
                SampleData.DtdlHabitablePlanet,
            ];
            await Client.CreateModelsAsync(models);
        }
        catch { }
    }

    [Fact]
    public async Task QueryAsync_SimpleQuery_ReturnsTwinsAndRelationships()
    {
        await IntializeAsync();
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

        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"
        MATCH (r:Twin { `$dtId`: 'room1' })-[rel:rel_has_sensors]->(s:Twin)
        RETURN r, rel, s
        "
            )
        )
        {
            Assert.NotNull(line);
            Assert.Equal(
                "room1",
                line.RootElement.GetProperty("r").GetProperty("$dtId").GetString()
            );
            Assert.Equal(
                "rel1",
                line.RootElement.GetProperty("rel").GetProperty("$relationshipId").GetString()
            );
            Assert.Equal(
                "sensor1",
                line.RootElement.GetProperty("rel").GetProperty("$targetId").GetString()
            );
            Assert.Equal(
                "sensor1",
                line.RootElement.GetProperty("s").GetProperty("$dtId").GetString()
            );
        }
    }

    [Fact]
    public async Task QueryAsync_RelationshipsQuery_ReturnsRelationship()
    {
        await IntializeAsync();
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

        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"
        MATCH (r:Twin)-[rel:rel_has_sensors]->(s:Twin)
        WHERE rel['$relationshipId'] = 'rel1'
        RETURN rel
        "
            )
        )
        {
            Assert.NotNull(line);
            Assert.Equal(
                "rel1",
                line.RootElement.GetProperty("rel").GetProperty("$relationshipId").GetString()
            );
            Assert.Equal(
                "sensor1",
                line.RootElement.GetProperty("rel").GetProperty("$targetId").GetString()
            );
        }
    }

    [Fact]
    public async Task QueryAsync_SimpleAdtQuery_ReturnsTwins()
    {
        await IntializeAsync();
        Dictionary<string, string> twins =
            new()
            {
                {
                    "room1",
                    @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
                },
                {
                    "room2",
                    @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"
        SELECT T FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "
            )
        )
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
        await IntializeAsync();
        Dictionary<string, string> twins =
            new()
            {
                {
                    "room1",
                    @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
                },
                {
                    "room2",
                    @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"
        SELECT _ FROM DIGITALTWINS _ WHERE _.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "
            )
        )
        {
            Assert.NotNull(line);
            var id = line.RootElement.GetProperty("$dtId").GetString();
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_SimpleAdtQuerySelectProperty_ReturnsPropertyValues()
    {
        await IntializeAsync();
        Dictionary<string, string> twins =
            new()
            {
                {
                    "room1",
                    @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
                },
                {
                    "room2",
                    @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"
        SELECT T.name FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "
            )
        )
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
        await IntializeAsync();
        Dictionary<string, string> twins =
            new()
            {
                {
                    "room1",
                    @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
                },
                {
                    "room2",
                    @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"
        SELECT T.name AS name FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "
            )
        )
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
        await IntializeAsync();
        Dictionary<string, string> twins =
            new()
            {
                {
                    "room1",
                    @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
                },
                {
                    "room2",
                    @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"
                },
                {
                    "room3",
                    @"{""$dtId"": ""room3"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 3""}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"
        SELECT TOP(1) T FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'
        "
            )
        )
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
        await IntializeAsync();
        Dictionary<string, string> twins =
            new()
            {
                {
                    "room1",
                    @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""notveryunique""}"
                },
                {
                    "room2",
                    @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""notveryunique""}"
                },
                {
                    "room3",
                    @"{""$dtId"": ""room3"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""notveryunique""}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"SELECT COUNT() FROM DIGITALTWINS T WHERE T.name = 'notveryunique'"
            )
        )
        {
            Assert.NotNull(line);
            Assert.Equal(3, line.RootElement.GetProperty("COUNT").GetInt16());
            count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task QueryAsync_SimpleAdtQuerySelectStar_ReturnsTwins()
    {
        await IntializeAsync();

        await foreach (
            var twin in Client.QueryAsync<JsonDocument>(
                @"SELECT * FROM DIGITALTWINS WHERE $metadata.$model = 'dtmi:com:adt:dtsample:room;1'"
            )
        )
        {
            await Client.DeleteDigitalTwinAsync(
                twin!.RootElement.GetProperty("$dtId")!.GetString()!
            );
        }

        Dictionary<string, string> twins =
            new()
            {
                {
                    "room1",
                    @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
                },
                {
                    "room2",
                    @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"SELECT * FROM DIGITALTWINS WHERE $metadata.$model = 'dtmi:com:adt:dtsample:room;1'"
            )
        )
        {
            Assert.NotNull(line);
            var id = line.RootElement.GetProperty("$dtId").GetString();
            var name = line.RootElement.GetProperty("name").GetString();
            Assert.StartsWith("Room", name);
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_IsOfModel_ReturnsTwins()
    {
        await IntializeAsync();

        await foreach (
            var twin in Client.QueryAsync<JsonDocument>(
                @"SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:adt:dtsample:room;1')"
            )
        )
        {
            await Client.DeleteDigitalTwinAsync(
                twin!.RootElement.GetProperty("$dtId")!.GetString()!
            );
        }

        Dictionary<string, string> twins =
            new()
            {
                {
                    "room1",
                    @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
                },
                {
                    "room2",
                    @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2""}"
                },
            };

        foreach (var twin in twins)
        {
            var t = await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
            Assert.NotNull(t);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:adt:dtsample:room;1') OR IS_OF_MODEL('dtmi:com:adt:dtsample:whatever;1')"
            )
        )
        {
            Assert.NotNull(line);
            var id = line.RootElement.GetProperty("$dtId").GetString();
            var name = line.RootElement.GetProperty("name").GetString();
            Assert.StartsWith("Room", name);
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_IsOfModel_ReturnsAllMatchingTwins()
    {
        await IntializeAsync();

        await foreach (var twin in Client.QueryAsync<JsonDocument>(@"SELECT * FROM DIGITALTWINS"))
        {
            await Client.DeleteDigitalTwinAsync(
                twin!.RootElement.GetProperty("$dtId")!.GetString()!
            );
        }

        Dictionary<string, string> twins =
            new()
            {
                {
                    "planet1",
                    @"{""$dtId"": ""planet1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:Planet;1""}, ""name"": ""Planet 1""}"
                },
                {
                    "celestialBody1",
                    @"{""$dtId"": ""celestialBody1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:CelestialBody;1""}, ""name"": ""Celestial Body 1"", ""mass"": 5.972e24}"
                },
                {
                    "habitablePlanet1",
                    @"{""$dtId"": ""habitablePlanet1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:HabitablePlanet;1""}, ""name"": ""Habitable Planet 1"", ""hasLife"": true}"
                },
            };

        foreach (var twin in twins)
        {
            var t = await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
            Assert.NotNull(t);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:contoso:CelestialBody;1')"
            )
        )
        {
            count++;
        }
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task QueryAsync_IsOfModel_ReturnsOnlyPlanets()
    {
        await IntializeAsync();

        Dictionary<string, string> twins =
            new()
            {
                {
                    "planet1",
                    @"{""$dtId"": ""planet1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:Planet;1""}, ""name"": ""Planet 1""}"
                },
                {
                    "celestialBody1",
                    @"{""$dtId"": ""celestialBody1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:CelestialBody;1""}, ""name"": ""Celestial Body 1"", ""mass"": 5.972e24}"
                },
                {
                    "habitablePlanet1",
                    @"{""$dtId"": ""habitablePlanet1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:HabitablePlanet;1""}, ""name"": ""Habitable Planet 1"", ""hasLife"": true}"
                },
            };

        foreach (var twin in twins)
        {
            var t = await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
            Assert.NotNull(t);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:contoso:Planet;1')"
            )
        )
        {
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_IsOfModel_ReturnsExactCelestialBody()
    {
        await IntializeAsync();

        Dictionary<string, string> twins =
            new()
            {
                {
                    "planet1",
                    @"{""$dtId"": ""planet1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:Planet;1""}, ""name"": ""Planet 1""}"
                },
                {
                    "celestialBody1",
                    @"{""$dtId"": ""celestialBody1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:CelestialBody;1""}, ""name"": ""Celestial Body 1"", ""mass"": 5.972e24}"
                },
                {
                    "habitablePlanet1",
                    @"{""$dtId"": ""habitablePlanet1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:HabitablePlanet;1""}, ""name"": ""Habitable Planet 1"", ""hasLife"": true}"
                },
            };

        foreach (var twin in twins)
        {
            var t = await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
            Assert.NotNull(t);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:contoso:CelestialBody;1', exact)"
            )
        )
        {
            count++;
        }
        Assert.Equal(1, count);
    }
}
