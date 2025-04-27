using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using Azure.DigitalTwins.Core;
using Json.Patch;
using Json.Pointer;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class DigitalTwinsTests : TestBase
{
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
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin!.Id, digitalTwin);

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(@"[{""op"": ""remove"", ""path"": ""/diameter""}]")!;
        // Remove first time
        await Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch);
        // Second removal should fail
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch));
    } */

    /* [Fact]
    public async Task UpdateDigitalTwinAsync_AddOperationInvalidProperty_ThrowsValidationException()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin!.Id, digitalTwin);

        JsonPatch jsonPatch = JsonSerializer.Deserialize<JsonPatch>(@"[{""op"": ""add"", ""path"": ""/invalidProperty"", ""value"": ""foo""}]")!;

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Client.UpdateDigitalTwinAsync(digitalTwin!.Id, jsonPatch));

        Assert.Contains("invalidProperty", exception.Message);
    } */
}
