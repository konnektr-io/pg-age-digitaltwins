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
    public async Task QueryAsync_IsObjectFunction_WorksAsExpected()
    {
        await IntializeAsync();
        var twinJson =
            @"{""$dtId"": ""typecheckroom1"", ""$metadata"": { ""$model"": ""dtmi:com:adt:dtsample:room;1"" }, ""name"": ""Room 1"", ""description"": ""This is a room."", ""temperature"": 22.5, ""humidity"": 0.5, ""dimensions"": { ""length"": 5.0, ""width"": 4.0, ""height"": 3.0 } }";
        await Client.CreateOrReplaceDigitalTwinAsync("typecheckroom1", twinJson);

        var objectResult = await Client
            .QueryAsync<JsonDocument>(
                @$"
            MATCH (t:Twin {{ `$dtId`: 'typecheckroom1' }})
            RETURN t.`$dtId` AS id, {Client.GetGraphName()}.is_object(t.dimensions) AS isObj, {Client.GetGraphName()}.is_object(t.name) AS isObjStr, {Client.GetGraphName()}.is_object(t.temperature) AS isObjNum
        "
            )
            .FirstOrDefaultAsync();
        Assert.NotNull(objectResult);
        Assert.True(objectResult.RootElement.GetProperty("isObj").GetBoolean());
        Assert.False(objectResult.RootElement.GetProperty("isObjStr").GetBoolean());
        Assert.False(objectResult.RootElement.GetProperty("isObjNum").GetBoolean());
    }

    [Fact]
    public async Task QueryAsync_IsPrimitiveFunction_WorksAsExpected()
    {
        await IntializeAsync();
        var twinJson =
            @"{""$dtId"": ""typecheckroom1"", ""$metadata"": { ""$model"": ""dtmi:com:adt:dtsample:room;1"" }, ""name"": ""Room 1"", ""description"": ""This is a room."", ""temperature"": 22.5, ""humidity"": 0.5, ""dimensions"": { ""length"": 5.0, ""width"": 4.0, ""height"": 3.0 } }";
        await Client.CreateOrReplaceDigitalTwinAsync("typecheckroom1", twinJson);

        var primResult = await Client
            .QueryAsync<JsonDocument>(
                $@"
            MATCH (t:Twin {{ `$dtId`: 'typecheckroom1' }})
            RETURN t.`$dtId` AS id, {Client.GetGraphName()}.is_primitive(t.dimensions) AS isPrimMap, {Client.GetGraphName()}.is_primitive(t.name) AS isPrimStr, {Client.GetGraphName()}.is_primitive(t.temperature) AS isPrimNum
        "
            )
            .FirstOrDefaultAsync();
        Assert.NotNull(primResult);
        Assert.False(primResult.RootElement.GetProperty("isPrimMap").GetBoolean());
        Assert.True(primResult.RootElement.GetProperty("isPrimStr").GetBoolean());
        Assert.True(primResult.RootElement.GetProperty("isPrimNum").GetBoolean());
    }

    [Fact]
    public async Task QueryAsync_IsStringFunction_WorksAsExpected()
    {
        await IntializeAsync();
        var twinJson =
            @"{""$dtId"": ""typecheckroom1"", ""$metadata"": { ""$model"": ""dtmi:com:adt:dtsample:room;1"" }, ""name"": ""Room 1"", ""description"": ""This is a room."", ""temperature"": 22.5, ""humidity"": 0.5, ""dimensions"": { ""length"": 5.0, ""width"": 4.0, ""height"": 3.0 } }";
        await Client.CreateOrReplaceDigitalTwinAsync("typecheckroom1", twinJson);

        var strResult = await Client
            .QueryAsync<JsonDocument>(
                @$"
            MATCH (t:Twin {{ `$dtId`: 'typecheckroom1' }})
            RETURN t.`$dtId` AS id, {Client.GetGraphName()}.is_string(t.dimensions) AS isStrMap, {Client.GetGraphName()}.is_string(t.name) AS isStrStr, {Client.GetGraphName()}.is_string(t.temperature) AS isStrNum
        "
            )
            .FirstOrDefaultAsync();
        Assert.NotNull(strResult);
        Assert.False(strResult.RootElement.GetProperty("isStrMap").GetBoolean());
        Assert.True(strResult.RootElement.GetProperty("isStrStr").GetBoolean());
        Assert.False(strResult.RootElement.GetProperty("isStrNum").GetBoolean());
    }

    [Fact]
    public async Task QueryAsync_TypeCheckFunctions_WorkAsExpected()
    {
        await IntializeAsync();
        // Insert a twin with a map, a string, and a number property
        var twinJson =
            @"{
            ""$dtId"": ""typecheckroom1"", 
            ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, 
            ""name"": ""Room 1"",
            ""description"": ""This is a room."",
            ""temperature"": 22.5,
            ""humidity"": 0.5,
            ""dimensions"": {
                ""length"": 5.0,
                ""width"": 4.0,
                ""height"": 3.0
            }
            }";
        await Client.CreateOrReplaceDigitalTwinAsync("typecheckroom1", twinJson);

        // IS_OBJECT should be true for mapProp, false for strProp, numProp
        var objectResult = await Client
            .QueryAsync<JsonDocument>(
                @$"
            MATCH (t:Twin {{ `$dtId`: 'typecheckroom1' }})
            RETURN t.`$dtId` AS id, {Client.GetGraphName()}.is_object(t.dimensions) AS isObj, {Client.GetGraphName()}.is_object(t.name) AS isObjStr, {Client.GetGraphName()}.is_object(t.temperature) AS isObjNum
        "
            )
            .FirstOrDefaultAsync();
        Assert.NotNull(objectResult);
        Assert.True(objectResult.RootElement.GetProperty("isObj").GetBoolean());
        Assert.False(objectResult.RootElement.GetProperty("isObjStr").GetBoolean());
        Assert.False(objectResult.RootElement.GetProperty("isObjNum").GetBoolean());

        // IS_PRIMITIVE should be true for strProp, numProp, false for mapProp
        var primResult = await Client
            .QueryAsync<JsonDocument>(
                $@"
            MATCH (t:Twin {{ `$dtId`: 'typecheckroom1' }})
            RETURN t.`$dtId` AS id, {Client.GetGraphName()}.is_primitive(t.dimensions) AS isPrimMap, {Client.GetGraphName()}.is_primitive(t.name) AS isPrimStr, {Client.GetGraphName()}.is_primitive(t.temperature) AS isPrimNum
        "
            )
            .FirstOrDefaultAsync();
        Assert.NotNull(primResult);
        Assert.False(primResult.RootElement.GetProperty("isPrimMap").GetBoolean());
        Assert.True(primResult.RootElement.GetProperty("isPrimStr").GetBoolean());
        Assert.True(primResult.RootElement.GetProperty("isPrimNum").GetBoolean());

        // IS_STRING should be true for strProp, false for mapProp, numProp
        var strResult = await Client
            .QueryAsync<JsonDocument>(
                @$"
            MATCH (t:Twin {{ `$dtId`: 'typecheckroom1' }})
            RETURN t.`$dtId` AS id, {Client.GetGraphName()}.is_string(t.dimensions) AS isStrMap, {Client.GetGraphName()}.is_string(t.name) AS isStrStr, {Client.GetGraphName()}.is_string(t.temperature) AS isStrNum
        "
            )
            .FirstOrDefaultAsync();
        Assert.NotNull(strResult);
        Assert.False(strResult.RootElement.GetProperty("isStrMap").GetBoolean());
        Assert.True(strResult.RootElement.GetProperty("isStrStr").GetBoolean());
        Assert.False(strResult.RootElement.GetProperty("isStrNum").GetBoolean());
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
    public async Task Performance_IsOfModel_CurrentImplementation()
    {
        await IntializeAsync();

        // Clean up any existing twins
        await foreach (var twin in Client.QueryAsync<JsonDocument>(@"SELECT * FROM DIGITALTWINS"))
        {
            await Client.DeleteDigitalTwinAsync(
                twin!.RootElement.GetProperty("$dtId")!.GetString()!
            );
        }

        // Diagnostic: Check if descendants are present in models before running test
        Console.WriteLine("\n=== Model Descendants Diagnostic ===");
        var graphName = Client.GetGraphName();
        await foreach (
            var model in Client.QueryAsync<JsonDocument>(
                $@"MATCH (m:Model) WHERE m.id IN ['dtmi:com:contoso:CelestialBody;1', 'dtmi:com:contoso:Planet;1', 'dtmi:com:contoso:HabitablePlanet;1'] RETURN m"
            )
        )
        {
            if (model != null && model.RootElement.TryGetProperty("m", out var mProp))
            {
                var modelId = mProp.GetProperty("id").GetString();
                if (mProp.TryGetProperty("descendants", out var descProp))
                {
                    Console.WriteLine(
                        $"  {modelId}: descendants = {descProp.GetRawText()} (length: {descProp.GetArrayLength()})"
                    );
                }
                else
                {
                    Console.WriteLine($"  {modelId}: NO descendants property found!");
                }
            }
        }
        Console.WriteLine();

        // Generate a large number of twins for scalability testing
        // Reduced from 750 to 250 to avoid timeouts during initial testing
        const int twinsPerType = 250;
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
                var result = await Client.CreateOrReplaceDigitalTwinsAsync<JsonObject>(batch!);
                Assert.False(result.HasFailures, $"Batch insert error at batch {i / batchSize}");
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

        // Test queries that will exercise inheritance lookup (current implementation only)
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

        const int iterations = 5; // Number of times to run each query for averaging

        var results = new List<(string name, long totalMs, int expectedCount, int actualCount)>();
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

            results.Add((name, totalTime, expectedCount, actualCount));
        }

        // Output performance results
        var output = new System.Text.StringBuilder();
        output.AppendLine("\n=== IS_OF_MODEL Performance ===");
        output.AppendLine($"Iterations per query: {iterations}");
        output.AppendLine($"Total twins in database: {twins.Count}");
        output.AppendLine();

        foreach (var (name, totalMs, expectedCount, actualCount) in results)
        {
            var avgMs = totalMs / (double)iterations;
            output.AppendLine(
                $"  {name}: {avgMs:F2}ms avg ({totalMs}ms total) - {actualCount}/{expectedCount} results"
            );
        }

        // Output to test console - this will show in test output
        Console.WriteLine(output.ToString());

        // For debugging purposes, also write to a temporary assertion that will always pass
        // but will show the results in the test output
        Assert.True(true, output.ToString());
    }

    [Fact]
    public async Task ExplainAnalyze_IsOfModel_ShowsQueryPlan()
    {
        await IntializeAsync();

        // Create a few test twins
        var twins = new Dictionary<string, string>
        {
            ["cb1"] =
                $"{{\"$dtId\": \"cb1\", \"$metadata\": {{\"$model\": \"dtmi:com:contoso:CelestialBody;1\"}}, \"name\": \"Celestial Body 1\", \"mass\": 1.0e24}}",
            ["p1"] =
                $"{{\"$dtId\": \"p1\", \"$metadata\": {{\"$model\": \"dtmi:com:contoso:Planet;1\"}}, \"name\": \"Planet 1\"}}",
            ["hp1"] =
                $"{{\"$dtId\": \"hp1\", \"$metadata\": {{\"$model\": \"dtmi:com:contoso:HabitablePlanet;1\"}}, \"name\": \"Habitable Planet 1\", \"hasLife\": true}}",
        };

        foreach (var (id, json) in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(id, json);
        }

        // Build the EXPLAIN ANALYZE query for IS_OF_MODEL
        // We need to test the actual Cypher query that uses the is_of_model function
        var graphName = Client.GetGraphName();
        var modelId = "dtmi:com:contoso:CelestialBody;1";

        // Apache AGE EXPLAIN ANALYZE syntax
        // Note: Using $cypher$ for dollar quoting to avoid conflicts with C# string interpolation
        var explainQuery =
            $@"
EXPLAIN (ANALYZE, VERBOSE, BUFFERS)
SELECT *
FROM ag_catalog.cypher('{graphName}', $cypher$
    MATCH (t:Twin)
    WHERE {graphName}.is_of_model(t, '{modelId}')
    RETURN t
$cypher$) AS (t agtype);";

        var output = new System.Text.StringBuilder();
        output.AppendLine("\n=== EXPLAIN ANALYZE for IS_OF_MODEL ===");
        output.AppendLine($"Model: {modelId}");
        output.AppendLine();

        // Execute EXPLAIN query directly through Npgsql
        await using var connection = await Client.GetDataSource().OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(explainQuery, connection);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var line = reader.GetString(0);
            output.AppendLine(line);
        }

        // Output the query plan
        var explainOutput = output.ToString();
        Console.WriteLine(explainOutput);

        // Check if the output contains information about the Model table access
        // We're looking for mentions of the Model table and index usage
        Assert.Contains("Model", explainOutput); // Should see Model table being accessed

        // The test passes if we got EXPLAIN output - we'll analyze it manually
        Assert.True(true, explainOutput);
    }

    [Fact]
    public async Task Performance_IsOfModel_DirectVsOptimized()
    {
        await IntializeAsync();

        // Clean up any existing twins
        await foreach (var twin in Client.QueryAsync<JsonDocument>(@"SELECT * FROM DIGITALTWINS"))
        {
            await Client.DeleteDigitalTwinAsync(
                twin!.RootElement.GetProperty("$dtId")!.GetString()!
            );
        }

        // Generate test twins
        const int twinsPerType = 250;
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

        // Bulk create twins
        const int batchSize = 100;
        var twinJsonObjects = twins
            .Values.Select(json => JsonNode.Parse(json)?.AsObject())
            .Where(obj => obj != null)
            .ToList();

        for (int i = 0; i < twinJsonObjects.Count; i += batchSize)
        {
            var batch = twinJsonObjects.Skip(i).Take(batchSize).ToList();
            var result = await Client.CreateOrReplaceDigitalTwinsAsync<JsonObject>(batch!);
            Assert.False(result.HasFailures, $"Batch insert error at batch {i / batchSize}");
        }

        var graphName = Client.GetGraphName();

        // Test queries: Direct function call vs Optimized CTE approach
        (string name, string modelId, int expectedCount)[] testCases = new[]
        {
            ("CelestialBody inheritance", "dtmi:com:contoso:CelestialBody;1", twinsPerType * 3),
            ("Planet inheritance", "dtmi:com:contoso:Planet;1", twinsPerType * 2),
            ("HabitablePlanet direct", "dtmi:com:contoso:HabitablePlanet;1", twinsPerType),
            ("Room direct", "dtmi:com:adt:dtsample:room;1", twinsPerType),
        };

        const int iterations = 5;

        var directResults =
            new List<(string name, long totalMs, int expectedCount, int actualCount)>();
        var optimizedResults =
            new List<(string name, long totalMs, int expectedCount, int actualCount)>();

        foreach (var (name, modelId, expectedCount) in testCases)
        {
            // Test DIRECT approach (using is_of_model function)
            var directQuery =
                $@"MATCH (t:Twin) WHERE {graphName}.is_of_model(t, '{modelId}') RETURN t";

            var directTotalTime = 0L;
            var directActualCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var count = 0;

                await foreach (var result in Client.QueryAsync<JsonDocument>(directQuery))
                {
                    count++;
                }

                stopwatch.Stop();
                directTotalTime += stopwatch.ElapsedMilliseconds;
                if (i == 0)
                    directActualCount = count;
            }

            Assert.Equal(expectedCount, directActualCount);
            directResults.Add((name, directTotalTime, expectedCount, directActualCount));

            // Test OPTIMIZED approach (pre-fetch descendants, then filter with IN)
            var optimizedQuery =
                $@"
MATCH (m:Model {{id: '{modelId}'}})
WITH m.descendants as model_ids
MATCH (t:Twin)
WHERE t.`$metadata`.`$model` = '{modelId}' OR t.`$metadata`.`$model` IN model_ids
RETURN t";

            var optimizedTotalTime = 0L;
            var optimizedActualCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var count = 0;

                await foreach (var result in Client.QueryAsync<JsonDocument>(optimizedQuery))
                {
                    count++;
                }

                stopwatch.Stop();
                optimizedTotalTime += stopwatch.ElapsedMilliseconds;
                if (i == 0)
                    optimizedActualCount = count;
            }

            Assert.Equal(expectedCount, optimizedActualCount);
            optimizedResults.Add((name, optimizedTotalTime, expectedCount, optimizedActualCount));
        }

        // Output performance comparison
        var output = new System.Text.StringBuilder();
        output.AppendLine("\n=== IS_OF_MODEL: Direct vs Optimized Performance ===");
        output.AppendLine($"Iterations per query: {iterations}");
        output.AppendLine($"Total twins in database: {twins.Count}");
        output.AppendLine();

        output.AppendLine("DIRECT (is_of_model function):");
        foreach (var (name, totalMs, expectedCount, actualCount) in directResults)
        {
            var avgMs = totalMs / (double)iterations;
            output.AppendLine(
                $"  {name}: {avgMs:F2}ms avg ({totalMs}ms total) - {actualCount}/{expectedCount} results"
            );
        }

        output.AppendLine();
        output.AppendLine("OPTIMIZED (pre-fetch descendants + IN):");
        foreach (var (name, totalMs, expectedCount, actualCount) in optimizedResults)
        {
            var avgMs = totalMs / (double)iterations;
            output.AppendLine(
                $"  {name}: {avgMs:F2}ms avg ({totalMs}ms total) - {actualCount}/{expectedCount} results"
            );
        }

        output.AppendLine();
        output.AppendLine("PERFORMANCE IMPROVEMENT:");
        for (int i = 0; i < directResults.Count; i++)
        {
            var directAvg = directResults[i].totalMs / (double)iterations;
            var optimizedAvg = optimizedResults[i].totalMs / (double)iterations;
            var improvement = ((directAvg - optimizedAvg) / directAvg) * 100;
            var speedup = directAvg / optimizedAvg;

            output.AppendLine(
                $"  {testCases[i].name}: {improvement:+0.0;-0.0}% improvement ({speedup:F1}x speedup)"
            );
        }

        Console.WriteLine(output.ToString());
        Assert.True(true, output.ToString());
    }

    [Fact]
    public async Task Performance_IsOfModel_WithModelAndDescendantsArray()
    {
        await IntializeAsync();

        // Clean up any existing twins
        await foreach (var twin in Client.QueryAsync<JsonDocument>(@"SELECT * FROM DIGITALTWINS"))
        {
            await Client.DeleteDigitalTwinAsync(
                twin!.RootElement.GetProperty("$dtId")!.GetString()!
            );
        }

        // Generate test twins
        const int twinsPerType = 250;
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

        // Bulk create twins
        const int batchSize = 100;
        var twinJsonObjects = twins
            .Values.Select(json => JsonNode.Parse(json)?.AsObject())
            .Where(obj => obj != null)
            .ToList();

        for (int i = 0; i < twinJsonObjects.Count; i += batchSize)
        {
            var batch = twinJsonObjects.Skip(i).Take(batchSize).ToList();
            var result = await Client.CreateOrReplaceDigitalTwinsAsync<JsonObject>(batch!);
            Assert.False(result.HasFailures, $"Batch insert error at batch {i / batchSize}");
        }

        var graphName = Client.GetGraphName();

        // Test queries: using is_of_model(model_and_descendants('{modelId}'))
        (string name, string modelId, int expectedCount)[] testCases = new[]
        {
            ("CelestialBody inheritance", "dtmi:com:contoso:CelestialBody;1", twinsPerType * 3),
            ("Planet inheritance", "dtmi:com:contoso:Planet;1", twinsPerType * 2),
            ("HabitablePlanet direct", "dtmi:com:contoso:HabitablePlanet;1", twinsPerType),
            ("Room direct", "dtmi:com:adt:dtsample:room;1", twinsPerType),
        };

        const int iterations = 5;
        var arrayArgResults =
            new List<(string name, long totalMs, int expectedCount, int actualCount)>();

        foreach (var (name, modelId, expectedCount) in testCases)
        {
            // Test using is_of_model with model_and_descendants array argument
            var arrayArgQuery =
                $@"MATCH (t:Twin) WHERE {graphName}.is_of_model(t, {graphName}.model_and_descendants('{modelId}')) RETURN t";

            var totalTime = 0L;
            var actualCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var count = 0;

                await foreach (var result in Client.QueryAsync<JsonDocument>(arrayArgQuery))
                {
                    count++;
                }

                stopwatch.Stop();
                totalTime += stopwatch.ElapsedMilliseconds;
                if (i == 0)
                    actualCount = count;
            }

            Assert.Equal(expectedCount, actualCount);
            arrayArgResults.Add((name, totalTime, expectedCount, actualCount));
        }

        // Output performance comparison
        var output = new System.Text.StringBuilder();
        output.AppendLine("\n=== IS_OF_MODEL: model_and_descendants(array arg) Performance ===");
        output.AppendLine($"Iterations per query: {iterations}");
        output.AppendLine($"Total twins in database: {twins.Count}");
        output.AppendLine();

        foreach (var (name, totalMs, expectedCount, actualCount) in arrayArgResults)
        {
            var avgMs = totalMs / (double)iterations;
            output.AppendLine(
                $"  {name}: {avgMs:F2}ms avg ({totalMs}ms total) - {actualCount}/{expectedCount} results"
            );
        }

        Console.WriteLine(output.ToString());
        Assert.True(true, output.ToString());
    }

    /* [Fact]
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
                var result = await Client.CreateOrReplaceDigitalTwinsAsync<JsonObject>(batch!);
                Assert.False(result.HasFailures, $"Batch insert error at batch {i / batchSize}");
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
    } */

    [Fact]
    public async Task QueryAsync_Collect_Vertices_ReturnsTwinPropertiesAsList()
    {
        // Verifies that collect(a), where a is a vertex, returns a JSON array of
        // twin property dicts — this was broken before the GetArray() fix because
        // the agtype list contained ::vertex-annotated items that are not valid JSON.
        await IntializeAsync();

        await Client.CreateOrReplaceDigitalTwinAsync(
            "room1",
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "sensor1",
            @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}"
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "sensor2",
            @"{""$dtId"": ""sensor2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 2"", ""temperature"": 30.0}"
        );
        await Client.CreateOrReplaceRelationshipAsync(
            "room1",
            "rel1",
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}"
        );
        await Client.CreateOrReplaceRelationshipAsync(
            "room1",
            "rel2",
            @"{""$relationshipId"": ""rel2"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor2""}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                @"MATCH (r:Twin { `$dtId`: 'room1' })
                  OPTIONAL MATCH (r)-[:rel_has_sensors]->(s:Twin)
                  RETURN r, collect(s) AS sensors"
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal("room1", result.RootElement.GetProperty("r").GetProperty("$dtId").GetString());

        var sensors = result.RootElement.GetProperty("sensors");
        Assert.Equal(JsonValueKind.Array, sensors.ValueKind);
        Assert.Equal(2, sensors.GetArrayLength());

        var dtIds = sensors
            .EnumerateArray()
            .Select(s => s.GetProperty("$dtId").GetString())
            .OrderBy(id => id)
            .ToList();
        Assert.Equal(new[] { "sensor1", "sensor2" }, dtIds);
    }

    [Fact]
    public async Task QueryAsync_Collect_Properties_ReturnsMapsAsList()
    {
        // Verifies that collect(properties(a)), the previous workaround for the
        // ::vertex annotation issue, still produces the correct result after the fix.
        await IntializeAsync();

        await Client.CreateOrReplaceDigitalTwinAsync(
            "room1",
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "sensor1",
            @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}"
        );
        await Client.CreateOrReplaceRelationshipAsync(
            "room1",
            "rel1",
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                @"MATCH (r:Twin { `$dtId`: 'room1' })
                  OPTIONAL MATCH (r)-[:rel_has_sensors]->(s:Twin)
                  RETURN r, collect(properties(s)) AS sensors"
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal("room1", result.RootElement.GetProperty("r").GetProperty("$dtId").GetString());

        var sensors = result.RootElement.GetProperty("sensors");
        Assert.Equal(JsonValueKind.Array, sensors.ValueKind);
        Assert.Equal(1, sensors.GetArrayLength());
        Assert.Equal("sensor1", sensors[0].GetProperty("$dtId").GetString());
        Assert.Equal(25.0, sensors[0].GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task QueryAsync_Collect_WithNoMatches_ReturnsEmptyList()
    {
        // Verifies that collect() returns an empty array when OPTIONAL MATCH finds nothing,
        // rather than throwing or returning null.
        await IntializeAsync();

        await Client.CreateOrReplaceDigitalTwinAsync(
            "room1",
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                @"MATCH (r:Twin { `$dtId`: 'room1' })
                  OPTIONAL MATCH (r)-[:rel_has_sensors]->(s:Twin)
                  RETURN r, collect(s) AS sensors"
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal("room1", result.RootElement.GetProperty("r").GetProperty("$dtId").GetString());

        var sensors = result.RootElement.GetProperty("sensors");
        Assert.Equal(JsonValueKind.Array, sensors.ValueKind);
        Assert.Equal(0, sensors.GetArrayLength());
    }

    [Fact]
    public async Task QueryAsync_Collect_Edges_ReturnsRelationshipPropertiesAsList()
    {
        await IntializeAsync();

        await Client.CreateOrReplaceDigitalTwinAsync(
            "room1",
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "sensor1",
            @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}"
        );
        await Client.CreateOrReplaceRelationshipAsync(
            "room1",
            "rel1",
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                @"MATCH (r:Twin { `$dtId`: 'room1' })-[e:rel_has_sensors]->(s:Twin)
                  RETURN r, collect(e) AS edges"
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal("room1", result.RootElement.GetProperty("r").GetProperty("$dtId").GetString());

        var edges = result.RootElement.GetProperty("edges");
        Assert.Equal(JsonValueKind.Array, edges.ValueKind);
        Assert.Equal(1, edges.GetArrayLength());

        var edge = edges[0];
        Assert.Equal("rel1", edge.GetProperty("$relationshipId").GetString());
        Assert.Equal("room1", edge.GetProperty("$sourceId").GetString());
        Assert.Equal("sensor1", edge.GetProperty("$targetId").GetString());
        Assert.Equal("rel_has_sensors", edge.GetProperty("$relationshipName").GetString());
    }

    [Fact]
    public async Task QueryAsync_Collect_MultipleColumns_ReturnsAllCorrectly()
    {
        await IntializeAsync();

        await Client.CreateOrReplaceDigitalTwinAsync(
            "room1",
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1""}"
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "sensor1",
            @"{""$dtId"": ""sensor1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 1"", ""temperature"": 25.0}"
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "sensor2",
            @"{""$dtId"": ""sensor2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor 2"", ""temperature"": 30.0}"
        );
        await Client.CreateOrReplaceRelationshipAsync(
            "room1",
            "rel1",
            @"{""$relationshipId"": ""rel1"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor1""}"
        );
        await Client.CreateOrReplaceRelationshipAsync(
            "room1",
            "rel2",
            @"{""$relationshipId"": ""rel2"", ""$sourceId"": ""room1"", ""$relationshipName"": ""rel_has_sensors"", ""$targetId"": ""sensor2""}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                @"MATCH (r:Twin { `$dtId`: 'room1' })
                  OPTIONAL MATCH (r)-[:rel_has_sensors]->(s:Twin)
                  RETURN collect(s) AS sensors, collect(properties(s)) AS props"
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);

        var sensors = result.RootElement.GetProperty("sensors");
        Assert.Equal(JsonValueKind.Array, sensors.ValueKind);
        Assert.Equal(2, sensors.GetArrayLength());

        var props = result.RootElement.GetProperty("props");
        Assert.Equal(JsonValueKind.Array, props.ValueKind);
        Assert.Equal(2, props.GetArrayLength());

        var sensorDtIds = sensors.EnumerateArray().Select(s => s.GetProperty("$dtId").GetString()).OrderBy(id => id).ToList();
        Assert.Equal(new[] { "sensor1", "sensor2" }, sensorDtIds);

        var propDtIds = props.EnumerateArray().Select(p => p.GetProperty("$dtId").GetString()).OrderBy(id => id).ToList();
        Assert.Equal(new[] { "sensor1", "sensor2" }, propDtIds);
    }

    [Fact]
    public async Task QueryAsync_Collect_Vertices_WithNestedProperties_ReturnsCorrectly()
    {
        await IntializeAsync();

        // Room has a dimensions property which is a nested object
        await Client.CreateOrReplaceDigitalTwinAsync(
            "room1",
            @"{""$dtId"": ""room1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 1"", ""dimensions"": {""length"": 5.0, ""width"": 4.0, ""height"": 3.0}}"
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "room2",
            @"{""$dtId"": ""room2"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room 2"", ""dimensions"": {""length"": 10.0, ""width"": 8.0, ""height"": 6.0}}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                @"MATCH (r:Twin)
                  WHERE r.`$metadata`.`$model` = 'dtmi:com:adt:dtsample:room;1'
                  RETURN collect(r) AS rooms"
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);

        var rooms = result.RootElement.GetProperty("rooms");
        Assert.Equal(JsonValueKind.Array, rooms.ValueKind);
        Assert.Equal(2, rooms.GetArrayLength());

        var room1 = rooms[0];
        Assert.Equal("room1", room1.GetProperty("$dtId").GetString());

        var dimensions = room1.GetProperty("dimensions");
        Assert.Equal(5.0, dimensions.GetProperty("length").GetDouble());
        Assert.Equal(4.0, dimensions.GetProperty("width").GetDouble());
        Assert.Equal(3.0, dimensions.GetProperty("height").GetDouble());

        var room2 = rooms[1];
        Assert.Equal("room2", room2.GetProperty("$dtId").GetString());

        var dimensions2 = room2.GetProperty("dimensions");
        Assert.Equal(10.0, dimensions2.GetProperty("length").GetDouble());
        Assert.Equal(8.0, dimensions2.GetProperty("width").GetDouble());
        Assert.Equal(6.0, dimensions2.GetProperty("height").GetDouble());
    }

    [Fact]
    public async Task QueryAsync_WithParameters_StringParam_ReturnsFilteredTwin()
    {
        await IntializeAsync();
        var twin =
            @"{""$dtId"": ""paramroom1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Param Room""}";
        await Client.CreateOrReplaceDigitalTwinAsync("paramroom1", twin);

        var result = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin { `$dtId`: $id }) RETURN t",
                new Dictionary<string, object?> { { "id", "paramroom1" } }
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal(
            "paramroom1",
            result.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );
    }

    [Fact]
    public async Task QueryAsync_WithParameters_NumericParam_ReturnsFilteredTwin()
    {
        await IntializeAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "numparam1",
            @"{""$dtId"": ""numparam1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Sensor A"", ""temperature"": 42.5}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.temperature > $minTemp RETURN t",
                new Dictionary<string, object?> { { "minTemp", 40.0 } }
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal(
            "numparam1",
            result.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );
    }

    [Fact]
    public async Task QueryAsync_WithParameters_BoolParam_ReturnsFilteredTwin()
    {
        await IntializeAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "boolparam1",
            @"{""$dtId"": ""boolparam1"", ""$metadata"": {""$model"": ""dtmi:com:contoso:HabitablePlanet;1""}, ""name"": ""Earth 2.0"", ""hasLife"": true}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.hasLife = $alive RETURN t",
                new Dictionary<string, object?> { { "alive", true } }
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal(
            "boolparam1",
            result.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );

        // Query with false should return nothing
        var noResult = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.hasLife = $alive RETURN t",
                new Dictionary<string, object?> { { "alive", false } }
            )
            .FirstOrDefaultAsync();

        Assert.Null(noResult);
    }

    [Fact]
    public async Task QueryAsync_WithParameters_MultipleParams_ReturnsFilteredTwin()
    {
        await IntializeAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "multiparam1",
            @"{""$dtId"": ""multiparam1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""}, ""name"": ""Multi Sensor"", ""temperature"": 36.0}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.temperature > $minTemp AND t.`$dtId` = $id RETURN t",
                new Dictionary<string, object?>
                {
                    { "minTemp", 30.0 },
                    { "id", "multiparam1" },
                }
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal(
            "multiparam1",
            result.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );
    }

    [Fact]
    public async Task QueryAsync_WithParameters_NullParam_ReturnsFilteredTwin()
    {
        await IntializeAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "nullparam1",
            @"{""$dtId"": ""nullparam1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room with null check""}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$dtId` = $id AND $val IS NULL RETURN t",
                new Dictionary<string, object?> { { "id", "nullparam1" }, { "val", null } }
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal(
            "nullparam1",
            result.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );

        // Verify that a non-null value in the same position would not match
        var noResult = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$dtId` = $id AND $val IS NULL RETURN t",
                new Dictionary<string, object?> { { "id", "nullparam1" }, { "val", "something" } }
            )
            .FirstOrDefaultAsync();

        Assert.Null(noResult);
    }

    [Fact]
    public async Task QueryAsync_WithParameters_ListParam_ReturnsFilteredTwin()
    {
        await IntializeAsync();
        Dictionary<string, string> twins = new()
        {
            { "listp1", "{\"$dtId\": \"listp1\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Room Alpha\"}" },
            { "listp2", "{\"$dtId\": \"listp2\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Room Beta\"}" },
            { "listp3", "{\"$dtId\": \"listp3\", \"$metadata\": {\"$model\": \"dtmi:com:adt:dtsample:room;1\"}, \"name\": \"Room Gamma\"}" },
        };

        foreach (var twin in twins)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(twin.Key, twin.Value);
        }

        int count = 0;
        await foreach (
            var line in Client.QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$dtId` IN $ids RETURN t",
                new Dictionary<string, object?>
                {
                    { "ids", new List<string> { "listp1", "listp3" } },
                }
            )
        )
        {
            Assert.NotNull(line);
            count++;
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryAsync_WithParameters_MapParam_ReturnsFilteredTwin()
    {
        await IntializeAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "mapparam1",
            @"{""$dtId"": ""mapparam1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room Map"", ""dimensions"": {""length"": 5.0, ""width"": 4.0, ""height"": 3.0}}"
        );

        // Use a map parameter to match the dimensions property
        var result = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.dimensions = $dims RETURN t",
                new Dictionary<string, object?>
                {
                    {
                        "dims",
                        new Dictionary<string, object?>
                        {
                            { "length", 5.0 },
                            { "width", 4.0 },
                            { "height", 3.0 },
                        }
                    },
                }
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal(
            "mapparam1",
            result.RootElement.GetProperty("t").GetProperty("$dtId").GetString()
        );
    }

    [Fact]
    public async Task QueryAsync_WithParameters_Pagination_PreservesParameters()
    {
        await IntializeAsync();

        // Create twins with names that can be filtered by parameter
        for (int i = 1; i <= 5; i++)
        {
            await Client.CreateOrReplaceDigitalTwinAsync(
                $"paramPageTwin{i}",
                $"{{\"$dtId\": \"paramPageTwin{i}\", \"$metadata\": {{\"$model\": \"dtmi:com:adt:dtsample:room;1\"}}, \"name\": \"Param Twin {i}\"}}"
            );
        }

        var prefix = "paramPageTwin";
        var firstPage = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$dtId` STARTS WITH $prefix RETURN t ORDER BY t.`$dtId`",
                new Dictionary<string, object?> { { "prefix", prefix } }
            )
            .AsPages(pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Value.Count());
        Assert.NotNull(firstPage.ContinuationToken);

        // Second page — the parameters should be carried forward by the continuation token
        var secondPage = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin) WHERE t.`$dtId` STARTS WITH $prefix RETURN t ORDER BY t.`$dtId`",
                null // no need to pass parameters again; they are in the continuation token
            )
            .AsPages(firstPage.ContinuationToken, pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Value.Count());
        Assert.NotNull(secondPage.ContinuationToken);

        // Third page
        var thirdPage = await Client
            .QueryAsync<JsonDocument>(
                string.Empty, // query is embedded in the continuation token
                null
            )
            .AsPages(secondPage.ContinuationToken, pageSizeHint: 2)
            .FirstAsync();

        Assert.NotNull(thirdPage);
        Assert.Single(thirdPage.Value);
        Assert.Null(thirdPage.ContinuationToken);
    }

    [Fact]
    public async Task QueryAsync_WithParameters_NoResults_ReturnsEmpty()
    {
        await IntializeAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "noroom1",
            @"{""$dtId"": ""noroom1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""Room""}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                "MATCH (t:Twin { `$dtId`: $id }) RETURN t",
                new Dictionary<string, object?> { { "id", "non_existent" } }
            )
            .FirstOrDefaultAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task QueryAsync_WithParameters_AdtQuery_WorksWithParameters()
    {
        // Parameters are only meaningful for raw Cypher queries, but should not break ADT queries.
        await IntializeAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "adtparam1",
            @"{""$dtId"": ""adtparam1"", ""$metadata"": {""$model"": ""dtmi:com:adt:dtsample:room;1""}, ""name"": ""ADT Param Room""}"
        );

        var result = await Client
            .QueryAsync<JsonDocument>(
                @"SELECT * FROM DIGITALTWINS WHERE $dtId = 'adtparam1'",
                new Dictionary<string, object?> { { "ignored", "value" } }
            )
            .FirstOrDefaultAsync();

        Assert.NotNull(result);
        Assert.Equal(
            "adtparam1",
            result.RootElement.GetProperty("$dtId").GetString()
        );
    }
}
