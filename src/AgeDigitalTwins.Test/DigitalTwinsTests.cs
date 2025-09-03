using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using Azure.DigitalTwins.Core;
using Json.More;
using Json.Patch;
using Json.Pointer;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class DigitalTwinsTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public DigitalTwinsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinAsync_BasicDigitalTwin_CreatedAndReadable()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        Assert.NotNull(createdTwin);
        Assert.Equal(digitalTwin.Id, createdTwin.Id);

        // Read digital twin
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(digitalTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);

        // Read it again with a different method, should have the same etag
        var readTwin2 = await Client.GetDigitalTwinAsync<JsonDocument>(readTwin.Id);
        Assert.NotNull(readTwin2);
        Assert.Equal(digitalTwin.Id, readTwin2.RootElement.GetProperty("$dtId").GetString());
        Assert.Equal(
            readTwin2.RootElement.GetProperty("$etag").GetString(),
            readTwin.ETag.ToString()
        );
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinAsync_BasicDigitalTwinWithWeirdcharacters_CreatedAndReadable()
    {
        // Load required models
        string[] models = [SampleData.DtdlRoom];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        string digitalTwinString =
            @"{
                ""$dtId"": ""room123456"",
                ""$metadata"": {
                    ""$model"": ""dtmi:com:adt:dtsample:room;1""
                },
                ""name"": ""Crater 1"",
                ""description"": ""A 'description' \""with a\n\rfew weird 👽 '/\\characters.""
            }";

        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(digitalTwinString);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        Assert.NotNull(createdTwin);
        Assert.Equal(digitalTwin.Id, createdTwin.Id);

        // Read digital twin
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(digitalTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinAsync_NoModel_ThrowsArgumentException()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = @"{""$dtId"": ""nomodeltwin"", ""test"": ""test""}";

        await Assert.ThrowsAsync<ArgumentException>(
            () => Client.CreateOrReplaceDigitalTwinAsync("nomodeltwin", digitalTwin)
        );
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinAsync_InvalidModel_ThrowsModelNotFoundException()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin =
            @"{""$dtId"": ""modelnotfoundtwin"", ""$metadata"": {""$model"": ""dtmi:com:notfound;1""}, ""test"": ""test""}";

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => Client.CreateOrReplaceDigitalTwinAsync("modelnotfoundtwin", digitalTwin)
        );
    }

    [Fact]
    public async Task CreateOrReplaceDigitalTwinAsync_InvalidProperty_ValidationFailedException()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin =
            @"{
            ""$dtId"": ""invalidtwin"", 
            ""$metadata"": {""$model"": ""dtmi:com:contoso:Crater;1""}, 
            ""test"": ""test"", 
            ""diameter"": ""foo""
        }";

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => Client.CreateOrReplaceDigitalTwinAsync("invalidtwin", digitalTwin)
        );

        Assert.Contains("test", exception.Message);
        Assert.Contains("diameter", exception.Message);
    }

    [Fact]
    public async Task UpdateDigitalTwinAsync_AddOperationPrimitive_Updated()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""add"", ""path"": ""/diameter"", ""value"": 200}]"
        )!;
        await Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch);

        // Read digital twin
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(digitalTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);
        Assert.Equal(
            (double)200,
            ((JsonElement)readTwin.Contents["diameter"]).TryGetDouble(out double diameter)
                ? diameter
                : 0
        );
    }

    [Fact]
    public async Task UpdateDigitalTwinAsync_RemoveOperationPrimitive_Updated()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""remove"", ""path"": ""/diameter""}]"
        )!;
        await Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch);

        // Read digital twin
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(digitalTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);
        Assert.DoesNotContain("diameter", readTwin.Contents);
    }

    [Fact]
    public async Task UpdateDigitalTwinAsync_MultipleOperations_Updated()
    {
        // Load required models
        string[] models =
        [
            SampleData.DtdlCelestialBody,
            SampleData.DtdlPlanet,
            SampleData.DtdlCrater,
        ];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinPlanetEarth);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        string sJsonPatch =
            @"[
            {
                ""op"": ""replace"",
                ""path"": ""/name"",
                ""value"": ""Earth 2""
            },
            {
                ""op"": ""add"",
                ""path"": ""/mass"",
                ""value"": 5.972E18
            }
        ]";

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(sJsonPatch)!;
        await Client.UpdateDigitalTwinAsync(createdTwin!.Id, jsonPatch);

        // Read digital twin
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(createdTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);
        Assert.Equal("Earth 2", readTwin.Contents["name"].ToString());
        Assert.Equal(5.972E18, ((JsonElement)readTwin.Contents["mass"]).GetDouble());
        Assert.NotNull(readTwin.LastUpdatedOn);
        Assert.Equal(
            readTwin.LastUpdatedOn,
            readTwin.Metadata.PropertyMetadata["name"].LastUpdatedOn
        );
    }

    [Fact]
    public async Task UpdateDigitalTwinsAsync_WithObject_UpdatedAndReadable()
    {
        // Load required models
        string[] models = [SampleData.DtdlRoom];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinRoom1);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        // Patch with object
        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""add"", ""path"": ""/dimensions"", ""value"": {
                    ""length"": 6.0,
                    ""width"": 5.0,
                    ""height"": 3
                }},
                {""op"": ""remove"", ""path"": ""/humidity""}]"
        )!;
        await Client.UpdateDigitalTwinAsync(createdTwin!.Id, jsonPatch);

        // Read digital twin
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(createdTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);
        Assert.Equal(
            (double)6.0,
            ((JsonElement)readTwin.Contents["dimensions"]).GetProperty("length").GetDouble()
        );
        Assert.Equal(
            (double)5.0,
            ((JsonElement)readTwin.Contents["dimensions"]).GetProperty("width").GetDouble()
        );
        Assert.False(readTwin.Contents.ContainsKey("humidity"));

        // Now patch nested property
        jsonPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""replace"", ""path"": ""/dimensions/length"", ""value"": 7.0}]"
        )!;
        await Client.UpdateDigitalTwinAsync(readTwin!.Id, jsonPatch);

        // Read digital twin
        var secondReadTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(readTwin.Id);
        Assert.NotNull(secondReadTwin);
        Assert.Equal(digitalTwin.Id, secondReadTwin.Id);
        Assert.Equal(
            (double)7.0,
            ((JsonElement)secondReadTwin.Contents["dimensions"]).GetProperty("length").GetDouble()
        );
        Assert.Equal(
            (double)5.0,
            ((JsonElement)secondReadTwin.Contents["dimensions"]).GetProperty("width").GetDouble()
        );
    }

    [Fact]
    public async Task UpdateDigitalTwinAsync_SourceTime_Updated()
    {
        // Load required models
        string[] models =
        [
            SampleData.DtdlCelestialBody,
            SampleData.DtdlPlanet,
            SampleData.DtdlCrater,
        ];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinPlanetEarth);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        Assert.NotNull(createdTwin);
        Assert.Equal(digitalTwin.Id, createdTwin.Id);

        var now = DateTime.UtcNow;
        var nowString = now.ToString("o");

        JsonPatch jsonPatch =
            new(
                PatchOperation.Add(JsonPointer.Parse("/name"), "Earth 3"),
                PatchOperation.Add(JsonPointer.Parse("/$metadata/name/sourceTime"), nowString)
            );

        await Client.UpdateDigitalTwinAsync(createdTwin.Id, jsonPatch);

        // Read digital twin
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(createdTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);
        Assert.Equal("Earth 3", readTwin.Contents["name"].ToString());
        Assert.True(
            (now - readTwin.Metadata.PropertyMetadata["name"].SourceTime) < TimeSpan.FromSeconds(1)
        );
        Assert.Equal(now, readTwin.Metadata.PropertyMetadata["name"].SourceTime);
    }

    /* [Fact]
    public async Task UpdateDigitalTwinAsync_RemoveAlreadyRemovedProperty_ThrowsException()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""remove"", ""path"": ""/diameter""}]"
        )!;
        // Remove first time
        await Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch);
        // Second removal should fail
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch)
        );
    } */

    [Fact]
    public async Task UpdateDigitalTwinAsync_AddOperationInvalidProperty_ThrowsValidationException()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""add"", ""path"": ""/invalidProperty"", ""value"": ""foo""}]"
        )!;

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch)
        );

        Assert.Contains("invalidProperty", exception.Message);
    }

    [Fact]
    public async Task UpdateDigitalTwinAsync_AddQueryWithSpecialCharacters_Updated()
    {
        // Load required models
        string[] models = [SampleData.DtdlQueryable];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinQueryable);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        // The query string with special characters that need proper escaping
        string queryValue =
            "MATCH (current:Twin)-[*1..2]->(T:Twin) WHERE current['$dtId']= '@_selectedAssessementGroupId' AND (digitaltwins.is_of_model(T,'dtmi:com:arcadis:climaterisk:Asset;1')) RETURN T.$dtId as Id, T.name as Name  ORDER BY Name ASC";

        // Create JSON patch - the JsonPatch library should handle the escaping
        JsonPatch jsonPatch = new(PatchOperation.Add(JsonPointer.Parse("/query"), queryValue));

        await Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch);

        // Read digital twin to verify the query was added correctly
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(digitalTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);
        Assert.True(readTwin.Contents.ContainsKey("query"));

        // Verify the query value is exactly what we set (no escaping issues)
        string actualQuery = readTwin.Contents["query"].ToString()!;
        Assert.Equal(queryValue, actualQuery);

        // Verify specific characters are preserved
        Assert.Contains("'$dtId'", actualQuery);
        Assert.Contains("'@_selectedAssessementGroupId'", actualQuery);
        Assert.Contains("'dtmi:com:arcadis:climaterisk:Asset;1'", actualQuery);
    }

    [Fact]
    public async Task UpdateDigitalTwinAsync_AddQueryWithSpecialCharacters_JsonDeserialization()
    {
        // Load required models
        string[] models = [SampleData.DtdlQueryable];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinQueryable);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        // Test with JsonSerializer.Deserialize approach to see if escaping is the issue
        string jsonPatchString =
            @"[{
            ""op"": ""add"",
            ""path"": ""/query"",
            ""value"": ""MATCH (current:Twin)-[*1..2]->(T:Twin) WHERE current['$dtId']= '@_selectedAssessementGroupId' AND (digitaltwins.is_of_model(T,'dtmi:com:arcadis:climaterisk:Asset;1')) RETURN T.$dtId as Id, T.name as Name  ORDER BY Name ASC""
        }]";

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(jsonPatchString)!;

        await Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch);

        // Read digital twin to verify the query was added correctly
        var readTwin = await Client.GetDigitalTwinAsync<BasicDigitalTwin>(digitalTwin.Id);
        Assert.NotNull(readTwin);
        Assert.Equal(digitalTwin.Id, readTwin.Id);
        Assert.True(readTwin.Contents.ContainsKey("query"));

        // Verify the query value is correctly stored
        string actualQuery = readTwin.Contents["query"].ToString()!;
        string expectedQuery =
            "MATCH (current:Twin)-[*1..2]->(T:Twin) WHERE current['$dtId']= '@_selectedAssessementGroupId' AND (digitaltwins.is_of_model(T,'dtmi:com:arcadis:climaterisk:Asset;1')) RETURN T.$dtId as Id, T.name as Name  ORDER BY Name ASC";
        Assert.Equal(expectedQuery, actualQuery);

        // Check for proper handling of single quotes and special characters
        Assert.Contains("'$dtId'", actualQuery);
        Assert.Contains("'@_selectedAssessementGroupId'", actualQuery);
        Assert.Contains("'dtmi:com:arcadis:climaterisk:Asset;1'", actualQuery);
    }

    [Fact]
    public async Task Benchmark_UpdateDigitalTwinAsync()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(
            digitalTwin!.Id,
            digitalTwin
        );

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""replace"", ""path"": ""/diameter"", ""value"": 123.45}]"
        )!;

        var stopwatch = new System.Diagnostics.Stopwatch();
        int iterations = 20;
        long totalMs = 0;

        for (int i = 0; i < iterations; i++)
        {
            stopwatch.Restart();
            await Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch);
            stopwatch.Stop();
            totalMs += stopwatch.ElapsedMilliseconds;
        }

        var avgMs = totalMs / (double)iterations;
        _output.WriteLine(
            $"Average UpdateDigitalTwinAsync time: {avgMs} ms over {iterations} runs"
        );
    }
}
