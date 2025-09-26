using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
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
    public async Task QueryAsync_AdtQueryWithTypeCheckFunctions_ReturnsTwins()
    {
        await IntializeAsync();
        Dictionary<string, string> twins =
            new()
            {
                {
                    "tempsensor1",
                    @"{""$dtId"": ""tempsensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Temperature Sensor 1"", ""temperature"": 22.5}"
                },
                {
                    "tempsensor2",
                    @"{""$dtId"": ""tempsensor2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Temperature Sensor 2""}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"SELECT T FROM DIGITALTWINS T WHERE IS_NUMBER(T.temperature)"
            )
        )
        {
            Assert.NotNull(line);
            var id = line.RootElement.GetProperty("T").GetProperty("$dtId").GetString();
            Assert.Equal("tempsensor1", id);
            count++;
        }
        Assert.Equal(1, count);

        int count1 = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                @"SELECT T FROM DIGITALTWINS T WHERE IS_NUMBER(T.name)"
            )
        )
        {
            Assert.NotNull(line);
            count1++;
        }
        Assert.Equal(0, count1);
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

    [Fact]
    public async Task QueryAsync_Pagination_ReturnsPaginatedResults()
    {
        await IntializeAsync();

        Dictionary<string, string> twins =
            new()
            {
                {
                    "twin1",
                    "{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 1\"}"
                },
                {
                    "twin2",
                    "{\"$dtId\": \"twin2\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 2\"}"
                },
                {
                    "twin3",
                    "{\"$dtId\": \"twin3\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 3\"}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        // First page
        var firstPage = await Client
            .QueryAsync<JsonDocument>(
                "SELECT * FROM DIGITALTWINS WHERE $metadata.$model = 'dtmi:com:adt:dtsample:room;1'"
            )
            .AsPages(pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Value.Count());
        Assert.NotNull(firstPage.ContinuationToken);

        // Second page
        var secondPage = await Client
            .QueryAsync<JsonDocument>(
                "SELECT * FROM DIGITALTWINS WHERE $metadata.$model = 'dtmi:com:adt:dtsample:room;1'"
            )
            .AsPages(firstPage.ContinuationToken, pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Value);
        Assert.Null(secondPage.ContinuationToken);

        // All results in one go
        int count = 0;
        await foreach (
            var twin in Client.QueryAsync<JsonDocument>(
                "SELECT * FROM DIGITALTWINS WHERE $metadata.$model = 'dtmi:com:adt:dtsample:room;1'"
            )
        )
        {
            Assert.NotNull(twin);
            count++;
        }
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task QueryAsync_Pagination_HandlesSmallerLimitInQuery()
    {
        await IntializeAsync();

        Dictionary<string, string> twins =
            new()
            {
                {
                    "twin1",
                    "{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 1\"}"
                },
                {
                    "twin2",
                    "{\"$dtId\": \"twin2\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 2\"}"
                },
                {
                    "twin3",
                    "{\"$dtId\": \"twin3\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 3\"}"
                },
                {
                    "twin4",
                    "{\"$dtId\": \"twin4\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 4\"}"
                },
                {
                    "twin5",
                    "{\"$dtId\": \"twin5\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 5\"}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        // Query 2 twins with a page size of 4
        var queryTwoPage = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$metadata`.`$model` = 'dtmi:com:adt:dtsample:room;1' RETURN t LIMIT 2"
            )
            .AsPages(pageSizeHint: 4)
            .FirstAsync();

        Assert.NotNull(queryTwoPage);
        Assert.Equal(2, queryTwoPage.Value.Count());
        Assert.Null(queryTwoPage.ContinuationToken);

        // Query 5 twins with a limit of 4
        var queryFivePages = Client
            .QueryAsync<JsonDocument>(
                "SELECT TOP(5) T FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'"
            )
            .AsPages(pageSizeHint: 2);

        int count = 0;
        await foreach (var page in queryFivePages)
        {
            Assert.NotNull(page);
            Assert.InRange(page.Value.Count(), 1, 2);
            count += page.Value.Count();
            Assert.True(count <= 5, "Count should never be higher than 5");
            if (count == 2)
            {
                Assert.NotNull(page.ContinuationToken);
            }
            else if (count == 5)
            {
                Assert.Null(page.ContinuationToken);
            }
        }
    }

    [Fact]
    public async Task QueryAsync_Pagination_HandlesBiggerLimitInQuery()
    {
        await IntializeAsync();

        Dictionary<string, string> twins =
            new()
            {
                {
                    "twin1",
                    "{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 1\"}"
                },
                {
                    "twin2",
                    "{\"$dtId\": \"twin2\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 2\"}"
                },
                {
                    "twin3",
                    "{\"$dtId\": \"twin3\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 3\"}"
                },
                {
                    "twin4",
                    "{\"$dtId\": \"twin4\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 4\"}"
                },
                {
                    "twin5",
                    "{\"$dtId\": \"twin5\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 5\"}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        // First page
        var firstPage = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$metadata`.`$model` = 'dtmi:com:adt:dtsample:room;1' RETURN t LIMIT 4"
            )
            .AsPages(pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Value.Count());
        Assert.NotNull(firstPage.ContinuationToken);

        // Second page
        var secondPage = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$metadata`.`$model` = 'dtmi:com:adt:dtsample:room;1' RETURN t LIMIT 4"
            )
            .AsPages(firstPage.ContinuationToken, pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Value.Count());
        Assert.Null(secondPage.ContinuationToken);
    }

    [Fact]
    public async Task QueryAsync_Pagination_HandlesSkipAndLimitInQuery()
    {
        await IntializeAsync();

        Dictionary<string, string> twins =
            new()
            {
                {
                    "twin1",
                    "{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 1\"}"
                },
                {
                    "twin2",
                    "{\"$dtId\": \"twin2\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 2\"}"
                },
                {
                    "twin3",
                    "{\"$dtId\": \"twin3\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 3\"}"
                },
                {
                    "twin4",
                    "{\"$dtId\": \"twin4\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 4\"}"
                },
                {
                    "twin5",
                    "{\"$dtId\": \"twin5\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Twin 5\"}"
                },
            };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        // First page
        var firstPage = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$metadata`.`$model` = 'dtmi:com:adt:dtsample:room;1' RETURN t SKIP 2 LIMIT 4"
            )
            .AsPages(pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Value.Count());
        Assert.Equal(
            "twin3",
            firstPage.Value.First()!.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );
        Assert.Equal(
            "twin4",
            firstPage.Value.Last()!.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );
        Assert.NotNull(firstPage.ContinuationToken);

        // Second page
        var secondPage = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$metadata`.`$model` = 'dtmi:com:adt:dtsample:room;1' RETURN t SKIP 2 LIMIT 4"
            )
            .AsPages(firstPage.ContinuationToken, pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Value);
        Assert.Equal(
            "twin5",
            secondPage.Value.First()!.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );
        Assert.Null(secondPage.ContinuationToken);
    }

    [Fact]
    public async Task QueryAsync_Pagination_HandlesMoreThanDefaultLimitResults()
    {
        await IntializeAsync();

        var numTwins = 2100;

        Dictionary<string, string> twins = new();
        for (int i = 1; i <= numTwins; i++)
        {
            twins[$"twin{i}"] =
                $"{{\"$dtId\": \"twin{i}\", \"$metadata\": {{\"$model\": \"dtmi:com:adt:dtsample:room;1\"}}, \"name\": \"Twin {i}\"}}";
        }

        // Bulk create twins in batches to avoid timeouts
        const int batchSize = 100;
        var twinJsonObjects = twins
            .Values.Select(json => JsonNode.Parse(json)?.AsObject())
            .Where(obj => obj != null)
            .ToList();

        for (int i = 0; i < twinJsonObjects.Count && i < 25; i += batchSize)
        {
            var batch = twinJsonObjects.Skip(i).Take(Math.Min(batchSize, 25 - i)).ToList();
            try
            {
                await Client.CreateOrReplaceDigitalTwinsAsync<JsonObject>(batch!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Batch insert error at batch {i / batchSize}: {ex.Message}");
                throw;
            }
        }

        // Query all  twins with a page size of 10
        var query = Client.QueryAsync<JsonDocument>(
            "SELECT * FROM DIGITALTWINS WHERE $metadata.$model = 'dtmi:com:adt:dtsample:room;1'"
        );

        int count = 0;
        await foreach (var page in query)
        {
            Assert.NotNull(page);
            count++;
        }
        Assert.Equal(numTwins, count);
    }

    [Fact]
    public async Task Performance_IsOfModel_NewVsOldImplementation()
    {
        await IntializeAsync();

        // Clean up any existing twins
        await foreach (var twin in Client.QueryAsync<JsonDocument>(@"SELECT * FROM DIGITALTWINS"))
        {
            await Client.DeleteDigitalTwinAsync(
                twin!.RootElement.GetProperty("$dtId")!.GetString()!
            );
        }

        // Generate a large number of twins for scalability testing
        const int twinsPerType = 750;
        var twins = new Dictionary<string, string>(twinsPerType * 4);

        for (int i = 1; i <= twinsPerType; i++)
        {
            twins[$"cb{i}"] =
                $"{{\"$dtId\": \"cb{i}\", \"$metadata\": {{\"$model\": \"dtmi:com:contoso:CelestialBody;1\"}}, \"name\": \"Celestial Body {i}\", \"mass\": {i}.0e24}}";
            twins[$"p{i}"] =
                $"{{\"$dtId\": \"p{i}\", \"$metadata\": {{\"$model\": \"dtmi:com:contoso:Planet;1\"}}, \"name\": \"Planet {i}\"}}";
            twins[$"hp{i}"] =
                $"{{\"$dtId\": \"hp{i}\", \"$metadata\": {{\"$model\": \"dtmi:com:contoso:HabitablePlanet;1\"}}, \"name\": \"Habitable Planet {i}\", \"hasLife\": {(i % 2 == 0 ? "false" : "true")}}}";
            twins[$"room{i}"] =
                $"{{\"$dtId\": \"room{i}\", \"$metadata\": {{\"$model\": \"dtmi:com:adt:dtsample:room;1\"}}, \"name\": \"Room {i}\"}}";
        }

        // Bulk create twins (in batches to avoid timeouts)
        const int batchSize = 100; // MaxBatchSize for CreateOrReplaceDigitalTwinsAsync
        var twinJsonObjects = twins
            .Values.Select(json => JsonNode.Parse(json)?.AsObject())
            .Where(obj => obj != null)
            .ToList();

        for (int i = 0; i < twinJsonObjects.Count; i += batchSize)
        {
            var batch = twinJsonObjects.Skip(i).Take(batchSize).ToList();
            try
            {
                await Client.CreateOrReplaceDigitalTwinsAsync<JsonObject>(batch!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Batch insert error at batch {i / batchSize}: {ex.Message}");
                throw;
            }
        }

        // Diagnostic: Count twins by model type before running performance queries
        var modelTypes = new[]
        {
            "dtmi:com:contoso:CelestialBody;1",
            "dtmi:com:contoso:Planet;1",
            "dtmi:com:contoso:HabitablePlanet;1",
            "dtmi:com:adt:dtsample:room;1",
        };
        var modelCounts = new Dictionary<string, int>();
        int totalTwins = 0;
        foreach (var model in modelTypes)
        {
            int count = 0;
            await foreach (
                var twin in Client.QueryAsync<JsonDocument>(
                    $"SELECT * FROM DIGITALTWINS WHERE $metadata.$model = '{model}'"
                )
            )
            {
                count++;
            }
            modelCounts[model] = count;
            totalTwins += count;
        }
        Console.WriteLine("\n=== Twin Counts by Model Type ===");
        foreach (var kvp in modelCounts)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine($"  Total twins: {totalTwins} (expected: {twins.Count})\n");
        Assert.Equal(twins.Count, totalTwins); // Ensure all twins are present

        // Diagnostic: Output model inheritance edges (CelestialBody, Planet, HabitablePlanet)
        var inheritanceModels = new[]
        {
            "dtmi:com:contoso:CelestialBody;1",
            "dtmi:com:contoso:Planet;1",
            "dtmi:com:contoso:HabitablePlanet;1",
        };
        Console.WriteLine("=== Model Inheritance Edges ===");
        foreach (var model in inheritanceModels)
        {
            int edgeCount = 0;
            await foreach (
                var edge in Client.QueryAsync<JsonDocument>(
                    $"MATCH (m:Model)-[e:_extends]->(parent:Model) WHERE m.id = '{model}' RETURN m, parent"
                )
            )
            {
                if (edge != null)
                {
                    if (
                        edge.RootElement.TryGetProperty("m", out var mProp)
                        && edge.RootElement.TryGetProperty("parent", out var parentProp)
                    )
                    {
                        string? mId = "(no id)";
                        if (mProp.TryGetProperty("id", out var mIdProp))
                        {
                            mId = mIdProp.GetString() ?? "(no id)";
                        }
                        string? parentId = "(no id)";
                        if (parentProp.TryGetProperty("id", out var parentIdProp))
                        {
                            parentId = parentIdProp.GetString() ?? "(no id)";
                        }
                        Console.WriteLine($"  {mId} EXTENDS {parentId}");
                        edgeCount++;
                    }
                }
            }
            if (edgeCount == 0)
                Console.WriteLine($"  {model} has no EXTENDS edges");
        }

        // Test queries that will exercise inheritance lookup (NEW implementation via ADT syntax)
        // cb + p + hp, p + hp, hp only, rooms only
        (string name, string query, int expectedCount)[] testQueries = new[]
        {
            (
                "CelestialBody inheritance query",
                "SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:contoso:CelestialBody;1')",
                twinsPerType * 3
            ),
            (
                "Planet inheritance query",
                "SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:contoso:Planet;1')",
                twinsPerType * 2
            ),
            (
                "HabitablePlanet direct query",
                "SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:contoso:HabitablePlanet;1')",
                twinsPerType
            ),
            (
                "Room direct query",
                "SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:adt:dtsample:room;1')",
                twinsPerType
            ),
        };

        var graphName = Client.GetGraphName();

        // Test with the old implementation (OLD implementation via direct Cypher calls)
        (string name, string query, int expectedCount)[] oldTestQueries = new[]
        {
            (
                "CelestialBody inheritance query (OLD)",
                $"MATCH (t:Twin) WHERE {graphName}.is_of_model_old(t, 'dtmi:com:contoso:CelestialBody;1') RETURN t",
                twinsPerType * 3
            ),
            (
                "Planet inheritance query (OLD)",
                $"MATCH (t:Twin) WHERE {graphName}.is_of_model_old(t, 'dtmi:com:contoso:Planet;1') RETURN t",
                twinsPerType * 2
            ),
            (
                "HabitablePlanet direct query (OLD)",
                $"MATCH (t:Twin) WHERE {graphName}.is_of_model_old(t, 'dtmi:com:contoso:HabitablePlanet;1') RETURN t",
                twinsPerType
            ),
            (
                "Room direct query (OLD)",
                $"MATCH (t:Twin) WHERE {graphName}.is_of_model_old(t, 'dtmi:com:adt:dtsample:room;1') RETURN t",
                twinsPerType
            ),
        };

        const int iterations = 5; // Number of times to run each query for averaging

        // Test new implementation
        var newResults =
            new List<(string name, long totalMs, int expectedCount, int actualCount)>();
        foreach (var (name, query, expectedCount) in testQueries)
        {
            var totalTime = 0L;
            var actualCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var count = 0;

                await foreach (var result in Client.QueryAsync<JsonDocument>(query))
                {
                    count++;
                }

                Assert.Equal(expectedCount, count); // Verify correctness

                stopwatch.Stop();
                totalTime += stopwatch.ElapsedMilliseconds;
                if (i == 0)
                    actualCount = count; // Save count from first iteration
            }

            newResults.Add((name, totalTime, expectedCount, actualCount));
        }

        // Test old implementation
        var oldResults =
            new List<(string name, long totalMs, int expectedCount, int actualCount)>();
        foreach (var (name, query, expectedCount) in oldTestQueries)
        {
            var totalTime = 0L;
            var actualCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var count = 0;

                await foreach (var result in Client.QueryAsync<JsonDocument>(query))
                {
                    count++;
                }

                Assert.Equal(expectedCount, count); // Verify correctness

                stopwatch.Stop();
                totalTime += stopwatch.ElapsedMilliseconds;
                if (i == 0)
                    actualCount = count; // Save count from first iteration
            }

            oldResults.Add((name, totalTime, expectedCount, actualCount));
        }

        // Output performance comparison
        var output = new System.Text.StringBuilder();
        output.AppendLine("\n=== IS_OF_MODEL Performance Comparison ===");
        output.AppendLine($"Iterations per query: {iterations}");
        output.AppendLine($"Total twins in database: {twins.Count}");
        output.AppendLine();

        output.AppendLine("NEW IMPLEMENTATION:");
        foreach (var (name, totalMs, expectedCount, actualCount) in newResults)
        {
            var avgMs = totalMs / (double)iterations;
            output.AppendLine(
                $"  {name}: {avgMs:F2}ms avg ({totalMs}ms total) - {actualCount}/{expectedCount} results"
            );
        }

        output.AppendLine();
        output.AppendLine("OLD IMPLEMENTATION:");
        foreach (var (name, totalMs, expectedCount, actualCount) in oldResults)
        {
            var avgMs = totalMs / (double)iterations;
            output.AppendLine(
                $"  {name}: {avgMs:F2}ms avg ({totalMs}ms total) - {actualCount}/{expectedCount} results"
            );
        }

        output.AppendLine();
        output.AppendLine("PERFORMANCE COMPARISON:");
        for (int i = 0; i < newResults.Count; i++)
        {
            var newAvg = newResults[i].totalMs / (double)iterations;
            var oldAvg = oldResults[i].totalMs / (double)iterations;
            var improvement = ((oldAvg - newAvg) / oldAvg) * 100;
            var speedup = oldAvg / newAvg;

            var direction = improvement > 0 ? "faster" : "slower";
            output.AppendLine(
                $"  Query {i + 1}: {improvement:+0.0;-0.0}% improvement ({speedup:F1}x {direction})"
            );
        }

        // Output to test console - this will show in test output
        Console.WriteLine(output.ToString());

        // For debugging purposes, also write to a temporary assertion that will always pass
        // but will show the results in the test output
        Assert.True(true, output.ToString());
    }
}
