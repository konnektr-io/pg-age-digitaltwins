using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using DTDLParser;
using Json.Patch;
using Json.Pointer;
using Npgsql;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class ModelsTests : TestBase
{
    [Fact]
    public async Task CreateModels_SingleModel_ValidatedAndCreated()
    {
        string[] models = [SampleData.DtdlRoom];
        var sampleDataJson = JsonDocument.Parse(SampleData.DtdlRoom);
        var sampleDataId = sampleDataJson.RootElement.GetProperty("@id").GetString();

        var results = await Client.CreateModelsAsync(models);

        for (int i = 0; i < models.Length; i++)
        {
            var resultJson = JsonDocument.Parse(results[i].DtdlModel!);
            var resultId = resultJson.RootElement.GetProperty("@id").GetString();

            Assert.Equal(sampleDataId, resultId);
        }

        var result = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.NotNull(result);
        var parsedResult = JsonDocument.Parse(result.DtdlModel!);
        Assert.Equal(sampleDataId, parsedResult.RootElement.GetProperty("@id").GetString());
    }

    [Fact]
    public async Task CreateModels_MultipleDependentModels_ValidatedAndCreated()
    {
        string[] models =
        [
            SampleData.DtdlPlanet,
            SampleData.DtdlCelestialBody,
            SampleData.DtdlCrater,
            SampleData.DtdlHabitablePlanet,
        ];
        var results = await Client.CreateModelsAsync(models);

        for (int i = 0; i < models.Length; i++)
        {
            var resultJson = JsonDocument.Parse(results[i].DtdlModel!);
            var sampleDataJson = JsonDocument.Parse(models[i]);

            var resultId = resultJson.RootElement.GetProperty("@id").GetString();
            var sampleDataId = sampleDataJson.RootElement.GetProperty("@id").GetString();

            Assert.Equal(sampleDataId, resultId);
            // Check if bases are correct
            if (resultId == "dtmi:com:contoso:Planet;1")
            {
                Assert.Single(results[i].Bases);
                Assert.Contains("dtmi:com:contoso:CelestialBody;1", results[i].Bases);
            }
            if (resultId == "dtmi:com:contoso:HabitablePlanet;1")
            {
                Assert.Equal(2, results[i].Bases.Length);
                Assert.Contains("dtmi:com:contoso:CelestialBody;1", results[i].Bases);
                Assert.Contains("dtmi:com:contoso:Planet;1", results[i].Bases);
            }
        }

        await foreach (
            var modelData in Client.GetModelsAsync(new() { IncludeModelDefinition = true })
        )
        {
            var modelId = modelData!.Id;
            var modelJson = JsonDocument.Parse(modelData.DtdlModel!);
            var modelIdFromJson = modelJson.RootElement.GetProperty("@id").GetString();
            Assert.Equal(modelId, modelIdFromJson);
            if (modelId == "dtmi:com:contoso:Planet;1")
            {
                Assert.Single(modelData.Bases);
                Assert.Contains("dtmi:com:contoso:CelestialBody;1", modelData.Bases);
            }
        }

        await foreach (
            var modelData in Client.GetModelsAsync(new() { IncludeModelDefinition = false })
        )
        {
            var modelId = modelData!.Id;
            Assert.Null(modelData.DtdlModel);
            if (modelId == "dtmi:com:contoso:Planet;1")
            {
                Assert.Single(modelData.Bases);
                Assert.Contains("dtmi:com:contoso:CelestialBody;1", modelData.Bases);
            }
        }

        bool providedModelIncluded = false;
        bool dependenciesIncluded = false;
        await foreach (
            var modelData in Client.GetModelsAsync(
                new()
                {
                    DependenciesFor = ["dtmi:com:contoso:Planet;1"],
                    IncludeModelDefinition = false,
                }
            )
        )
        {
            var modelId = modelData!.Id;
            Assert.Null(modelData.DtdlModel);
            if (modelId == "dtmi:com:contoso:Planet;1")
            {
                providedModelIncluded = true;
                Assert.Single(modelData.Bases);
                Assert.Contains("dtmi:com:contoso:CelestialBody;1", modelData.Bases);
            }
            if (modelId == "dtmi:com:contoso:CelestialBody;1")
            {
                dependenciesIncluded = true;
            }
        }
        Assert.True(providedModelIncluded);
        Assert.True(dependenciesIncluded);
    }

    [Fact]
    public async Task CreateModels_MultipleDependentModelsResolveInDb_ValidatedAndCreated()
    {
        await Client.CreateModelsAsync([SampleData.DtdlCelestialBody, SampleData.DtdlCrater]);

        string[] models = [SampleData.DtdlPlanet];
        var results = await Client.CreateModelsAsync(models);

        for (int i = 0; i < models.Length; i++)
        {
            var resultJson = JsonDocument.Parse(results[i].DtdlModel!);
            var sampleDataJson = JsonDocument.Parse(models[i]);

            var resultId = resultJson.RootElement.GetProperty("@id").GetString();
            var sampleDataId = sampleDataJson.RootElement.GetProperty("@id").GetString();

            Assert.Equal(sampleDataId, resultId);
        }
    }

    [Fact]
    public async Task CreateModels_MissingDependency_ThrowsFailedToResolve()
    {
        // First make sure to delete the dependent models
        try
        {
            await Client.DeleteModelAsync("dtmi:com:contoso:CelestialBody;1");
        }
        catch (ModelNotFoundException)
        {
            // Ignore exception if model does not exist
        }
        try
        {
            await Client.DeleteModelAsync("dtmi:com:contoso:Crater;1");
        }
        catch (ModelNotFoundException)
        {
            // Ignore exception if model does not exist
        }

        bool exceptionThrown = false;
        try
        {
            string[] models = [SampleData.DtdlPlanet];
            var results = await Client.CreateModelsAsync(models);
        }
        catch (Exception ex)
        {
            exceptionThrown = true;
            Assert.IsType<ResolutionException>(ex);
            Assert.Contains("failed to resolve", ex.Message);
            Assert.Contains("dtmi:com:contoso:CelestialBody;1", ex.Message);
            Assert.Contains("dtmi:com:contoso:Crater;1", ex.Message);
        }
        Assert.True(exceptionThrown);
    }

    [Fact]
    public async Task DeleteModels_DeletesModelsWithNoDependencies()
    {
        // First delete everything
        string[] modelIds =
        [
            "dtmi:com:contoso:Planet;1",
            "dtmi:com:contoso:CelestialBody;1",
            "dtmi:com:contoso:Crater;1",
        ];
        foreach (var modelId in modelIds)
        {
            try
            {
                await Client.DeleteModelAsync(modelId);
            }
            catch
            {
                // Ignore exception if model does not exist
            }
        }

        // Create all models again
        await Client.CreateModelsAsync(
            [SampleData.DtdlCelestialBody, SampleData.DtdlCrater, SampleData.DtdlPlanet]
        );

        await Client.DeleteModelAsync("dtmi:com:contoso:Planet;1");

        bool exceptionThrown = false;
        try
        {
            var result = await Client.GetModelAsync("dtmi:com:contoso:Planet;1");
        }
        catch (ModelNotFoundException)
        {
            exceptionThrown = true;
        }
        Assert.True(exceptionThrown);
    }

    [Fact]
    public async Task DeleteModels_ThrowsWhenModelReferencesAreNotDeleted()
    {
        // First delete any existing models to avoid conflicts (in correct order)
        string[] modelIds =
        [
            "dtmi:com:contoso:Planet;1",
            "dtmi:com:contoso:CelestialBody;1",
            "dtmi:com:contoso:Crater;1",
        ];
        foreach (var modelId in modelIds)
        {
            try
            {
                await Client.DeleteModelAsync(modelId);
            }
            catch
            {
                // Ignore exception if model does not exist
            }
        }

        await Client.CreateModelsAsync(
            [SampleData.DtdlCelestialBody, SampleData.DtdlCrater, SampleData.DtdlPlanet]
        );

        bool exceptionThrown = false;

        try
        {
            await Client.DeleteModelAsync("dtmi:com:contoso:Crater;1");
        }
        catch (ModelReferencesNotDeletedException)
        {
            exceptionThrown = true;
        }
        Assert.True(exceptionThrown);
    }

    [Fact]
    public async Task CreateModels_ExistingModel_ThrowsModelAlreadyExists()
    {
        try
        {
            await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
        }
        catch (ModelNotFoundException)
        {
            // Ignore exception if model does not exist
        }

        await Client.CreateModelsAsync([SampleData.DtdlRoom]);

        bool exceptionThrown = false;
        try
        {
            await Client.CreateModelsAsync([SampleData.DtdlRoom]);
        }
        catch (Exception ex)
        {
            exceptionThrown = true;
            Assert.IsType<ModelAlreadyExistsException>(ex);
        }
        Assert.True(exceptionThrown);
    }

    [Fact]
    public async Task CreateModels_CanDeleteAndCreateAgain()
    {
        try
        {
            await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
        }
        catch (ModelNotFoundException)
        {
            // Ignore exception if model does not exist
        }
        var m1 = await Client.CreateModelsAsync([SampleData.DtdlRoom]);
        Assert.Equal("dtmi:com:adt:dtsample:room;1", m1[0].Id);
        await Client.DeleteModelAsync(m1[0].Id);
        var m2 = await Client.CreateModelsAsync([SampleData.DtdlRoom]);
        Assert.Equal("dtmi:com:adt:dtsample:room;1", m2[0].Id);
        await Client.DeleteModelAsync(m2[0].Id);
    }

    [Fact]
    public async Task GetModelIdByTwinId_ShouldReturnCorrectModelId()
    {
        // Arrange
        string modelId = "dtmi:com:adt:dtsample:room;1";
        try
        {
            await Client.DeleteModelAsync(modelId);
        }
        catch (ModelNotFoundException)
        {
            // Ignore exception if model does not exist
        }

        // Create the model
        await Client.CreateModelsAsync([SampleData.DtdlRoom]);

        // Create a digital twin with the model
        string twinId = "test-room-model-id";
        string twinJson = $$"""
            {
              "$dtId": "{{twinId}}",
              "$metadata": { "$model": "{{modelId}}" },
              "name": "Test Room",
              "temperature": 20.0
            }
            """;

        await Client.CreateOrReplaceDigitalTwinAsync(twinId, twinJson);

        // Act - Use reflection to access the private method
        var method = typeof(AgeDigitalTwinsClient).GetMethod(
            "GetModelIdByTwinIdAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        Assert.NotNull(method);

        var task = method.Invoke(Client, [twinId, CancellationToken.None]) as Task<string>;
        Assert.NotNull(task);
        var result = await task;

        // Assert
        Assert.Equal(modelId, result);

        // Clean up
        await Client.DeleteDigitalTwinAsync(twinId);
        await Client.DeleteModelAsync(modelId);
    }

    [Fact]
    public async Task DeleteAllModels_DeletesAllModels()
    {
        // Arrange: Create a few models
        string[] models =
        [
            SampleData.DtdlPlanet,
            SampleData.DtdlCelestialBody,
            SampleData.DtdlCrater,
            SampleData.DtdlHabitablePlanet,
        ];
        await Client.CreateModelsAsync(models);

        // Act: Delete all models
        await Client.DeleteAllModelsAsync();

        // Assert: No models should remain
        bool anyModelsExist = false;
        await foreach (
            var modelData in Client.GetModelsAsync(new() { IncludeModelDefinition = false })
        )
        {
            anyModelsExist = true;
            break;
        }
        Assert.False(anyModelsExist);
    }

    [Fact]
    public async Task CreateModels_DescendantsAndBasesStoredCorrectly()
    {
        // Clean up existing models
        string[] modelIds =
        [
            "dtmi:com:contoso:HabitablePlanet;1",
            "dtmi:com:contoso:Planet;1",
            "dtmi:com:contoso:CelestialBody;1",
            "dtmi:com:contoso:Crater;1",
        ];
        foreach (var modelId in modelIds)
        {
            try
            {
                await Client.DeleteModelAsync(modelId);
            }
            catch (ModelNotFoundException)
            {
                // Ignore if model doesn't exist
            }
        }

        // Create models with inheritance hierarchy:
        // CelestialBody (base)
        // └─ Planet (extends CelestialBody)
        //    └─ HabitablePlanet (extends Planet, which transitively extends CelestialBody)
        string[] models =
        [
            SampleData.DtdlCelestialBody,
            SampleData.DtdlCrater,
            SampleData.DtdlPlanet,
            SampleData.DtdlHabitablePlanet,
        ];
        var results = await Client.CreateModelsAsync(models);

        // Verify CelestialBody
        var celestialBody = results.FirstOrDefault(m => m.Id == "dtmi:com:contoso:CelestialBody;1");
        Assert.NotNull(celestialBody);
        Assert.Empty(celestialBody.Bases); // CelestialBody has no bases
        Assert.NotNull(celestialBody.Descendants);
        Assert.Equal(2, celestialBody.Descendants!.Length); // Planet and HabitablePlanet
        Assert.Contains("dtmi:com:contoso:Planet;1", celestialBody.Descendants);
        Assert.Contains("dtmi:com:contoso:HabitablePlanet;1", celestialBody.Descendants);

        // Verify Planet
        var planet = results.FirstOrDefault(m => m.Id == "dtmi:com:contoso:Planet;1");
        Assert.NotNull(planet);
        Assert.Single(planet.Bases);
        Assert.Contains("dtmi:com:contoso:CelestialBody;1", planet.Bases);
        Assert.NotNull(planet.Descendants);
        Assert.Single(planet.Descendants!); // Only HabitablePlanet
        Assert.Contains("dtmi:com:contoso:HabitablePlanet;1", planet.Descendants);

        // Verify HabitablePlanet
        var habitablePlanet = results.FirstOrDefault(m =>
            m.Id == "dtmi:com:contoso:HabitablePlanet;1"
        );
        Assert.NotNull(habitablePlanet);
        Assert.Equal(2, habitablePlanet.Bases.Length);
        Assert.Contains("dtmi:com:contoso:CelestialBody;1", habitablePlanet.Bases);
        Assert.Contains("dtmi:com:contoso:Planet;1", habitablePlanet.Bases);
        Assert.NotNull(habitablePlanet.Descendants);
        Assert.Empty(habitablePlanet.Descendants!); // No descendants

        // Verify Crater (independent model with component relationship)
        var crater = results.FirstOrDefault(m => m.Id == "dtmi:com:contoso:Crater;1");
        Assert.NotNull(crater);
        Assert.Empty(crater.Bases);
        Assert.NotNull(crater.Descendants);
        Assert.Empty(crater.Descendants!);
    }

    [Fact]
    public async Task CreateModels_DescendantsPersistedInDatabase()
    {
        // Clean up existing models
        string[] modelIds =
        [
            "dtmi:com:contoso:HabitablePlanet;1",
            "dtmi:com:contoso:Planet;1",
            "dtmi:com:contoso:CelestialBody;1",
            "dtmi:com:contoso:Crater;1",
        ];
        foreach (var modelId in modelIds)
        {
            try
            {
                await Client.DeleteModelAsync(modelId);
            }
            catch (ModelNotFoundException)
            {
                // Ignore if model doesn't exist
            }
        }

        // Create models
        await Client.CreateModelsAsync(
            [
                SampleData.DtdlCelestialBody,
                SampleData.DtdlCrater,
                SampleData.DtdlPlanet,
                SampleData.DtdlHabitablePlanet,
            ]
        );

        // Retrieve models from database to verify persistence
        var celestialBody = await Client.GetModelAsync("dtmi:com:contoso:CelestialBody;1");
        Assert.NotNull(celestialBody.Descendants);
        Assert.Equal(2, celestialBody.Descendants!.Length);
        Assert.Contains("dtmi:com:contoso:Planet;1", celestialBody.Descendants);
        Assert.Contains("dtmi:com:contoso:HabitablePlanet;1", celestialBody.Descendants);

        var planet = await Client.GetModelAsync("dtmi:com:contoso:Planet;1");
        Assert.NotNull(planet.Descendants);
        Assert.Single(planet.Descendants!);
        Assert.Contains("dtmi:com:contoso:HabitablePlanet;1", planet.Descendants);

        var habitablePlanet = await Client.GetModelAsync("dtmi:com:contoso:HabitablePlanet;1");
        Assert.NotNull(habitablePlanet.Descendants);
        Assert.Empty(habitablePlanet.Descendants!);

        // Also verify via raw Cypher query to ensure database storage
        var graphName = Client.GetGraphName();
        var celestialBodyRaw = await Client
            .QueryAsync<JsonDocument>(
                $@"MATCH (m:Model {{id: 'dtmi:com:contoso:CelestialBody;1'}}) RETURN m"
            )
            .FirstOrDefaultAsync();
        Assert.NotNull(celestialBodyRaw);
        var descendants = celestialBodyRaw.RootElement.GetProperty("m").GetProperty("descendants");
        Assert.Equal(JsonValueKind.Array, descendants.ValueKind);
        Assert.Equal(2, descendants.GetArrayLength());
    }

    [Fact]
    public async Task CreateModels_DescendantsUpdatedInBaseModel_WhenDerivedModelsCreatedLater()
    {
        // Clean up existing models
        string[] modelIds =
        [
            "dtmi:com:contoso:HabitablePlanet;1",
            "dtmi:com:contoso:Planet;1",
            "dtmi:com:contoso:CelestialBody;1",
            "dtmi:com:contoso:Crater;1",
        ];
        foreach (var modelId in modelIds)
        {
            try
            {
                await Client.DeleteModelAsync(modelId);
            }
            catch (ModelNotFoundException)
            {
                // Ignore if model doesn't exist
            }
        }

        // First batch: Create base model only
        await Client.CreateModelsAsync([SampleData.DtdlCelestialBody]);

        // Verify base model has no descendants initially
        var celestialBodyBefore = await Client.GetModelAsync("dtmi:com:contoso:CelestialBody;1");
        Assert.NotNull(celestialBodyBefore.Descendants);
        Assert.Empty(celestialBodyBefore.Descendants!);

        // Second batch: Create Planet (extends CelestialBody) and Crater
        await Client.CreateModelsAsync([SampleData.DtdlCrater, SampleData.DtdlPlanet]);

        // Verify base model now has Planet as descendant
        var celestialBodyAfterPlanet = await Client.GetModelAsync(
            "dtmi:com:contoso:CelestialBody;1"
        );
        Assert.NotNull(celestialBodyAfterPlanet.Descendants);
        Assert.Single(celestialBodyAfterPlanet.Descendants!);
        Assert.Contains("dtmi:com:contoso:Planet;1", celestialBodyAfterPlanet.Descendants);

        // Third batch: Create HabitablePlanet (extends Planet, which extends CelestialBody)
        await Client.CreateModelsAsync([SampleData.DtdlHabitablePlanet]);

        // Verify base model now has both Planet and HabitablePlanet as descendants
        var celestialBodyFinal = await Client.GetModelAsync("dtmi:com:contoso:CelestialBody;1");
        Assert.NotNull(celestialBodyFinal.Descendants);
        Assert.Equal(2, celestialBodyFinal.Descendants!.Length);
        Assert.Contains("dtmi:com:contoso:Planet;1", celestialBodyFinal.Descendants);
        Assert.Contains("dtmi:com:contoso:HabitablePlanet;1", celestialBodyFinal.Descendants);

        // Verify Planet also has HabitablePlanet as descendant
        var planetFinal = await Client.GetModelAsync("dtmi:com:contoso:Planet;1");
        Assert.NotNull(planetFinal.Descendants);
        Assert.Single(planetFinal.Descendants!);
        Assert.Contains("dtmi:com:contoso:HabitablePlanet;1", planetFinal.Descendants);
    }

    [Fact]
    public async Task UpdateModel_Decommissioned_Success()
    {
        try
        {
            await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
        }
        catch (ModelNotFoundException)
        {
        }

        await Client.CreateModelsAsync([SampleData.DtdlRoom]);

        var model = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.False(model.IsDecommissioned);

        JsonPatch decommissionPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""replace"", ""path"": ""/decommissioned"", ""value"": true}]"
        )!;
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:room;1", decommissionPatch);

        model = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.True(model.IsDecommissioned);

        JsonPatch recommissionPatch = JsonSerializer.Deserialize<JsonPatch>(
            @"[{""op"": ""replace"", ""path"": ""/decommissioned"", ""value"": false}]"
        )!;
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:room;1", recommissionPatch);

        model = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.False(model.IsDecommissioned);
    }

    [CnpgOnlyFact]
    public async Task UpdateModel_Embedding_Success()
    {
        try
        {
            await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
        }
        catch (ModelNotFoundException)
        {
        }

        await Client.CreateModelsAsync([SampleData.DtdlRoom]);

        var model = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.Null(model.Embedding);

        double[] expectedEmbedding = [0.1, 0.2, 0.3];
        var embeddingNode = JsonSerializer.SerializeToNode(expectedEmbedding);
        JsonPatch embeddingPatch = new(
            PatchOperation.Replace(JsonPointer.Parse("/embedding"), embeddingNode)
        );
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:room;1", embeddingPatch);

        model = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.NotNull(model.Embedding);
        Assert.Equal(expectedEmbedding.Length, model.Embedding.Length);
        for (int i = 0; i < expectedEmbedding.Length; i++)
        {
            Assert.Equal(expectedEmbedding[i], model.Embedding[i]);
        }
    }

    [Fact]
    public async Task GetModelAsync_IncludesAllBaseProperties_WhenIncludeBaseModelContentsTrue()
    {
        // Arrange: Clean up and create base and derived models
        await Client.DeleteAllModelsAsync();
        string[] models =
        {
            SampleData.DtdlCelestialBody,
            SampleData.DtdlPlanet,
            SampleData.DtdlCrater,
        };
        await Client.CreateModelsAsync(models);

        // Act: Get the derived model with base contents included
        var result = await Client.GetModelAsync(
            "dtmi:com:contoso:Planet;1",
            new() { IncludeBaseModelContents = true }
        );

        // Assert: All properties from both base and derived should be present
        Assert.NotNull(result.Properties);
        var props = result.Properties;
        var propNames = props.Select(p => p.GetProperty("name").GetString()).ToList();
        // CelestialBody: name, mass, temperature; Planet: hasLife
        Assert.Contains("name", propNames);
        Assert.Contains("mass", propNames);
        Assert.Contains("temperature", propNames);
        Assert.Contains("hasLife", propNames);
        // Should not contain duplicates
        Assert.Equal(4, propNames.Distinct().Count());

        // Check relationships
        Assert.NotNull(result.Relationships);
        var relNames = result.Relationships.Select(r => r.GetProperty("name").GetString()).ToList();
        // CelestialBody: orbits; Planet: satellites
        Assert.Contains("orbits", relNames);
        Assert.Contains("satellites", relNames);
        Assert.Equal(2, relNames.Distinct().Count());

        // Act: Check that base model alone also works
        var result2 = await Client.GetModelAsync(
            "dtmi:com:contoso:CelestialBody;1",
            new() { IncludeBaseModelContents = true }
        );
        // Assert: All properties should be present
        Assert.NotNull(result2.Properties);
        var props2 = result2.Properties;
        var propNames2 = props2.Select(p => p.GetProperty("name").GetString()).ToList();
        Assert.Contains("name", propNames2);
        Assert.Contains("mass", propNames2);
        Assert.Contains("temperature", propNames2);
        Assert.Equal(3, propNames2.Distinct().Count());
        // Assert: Check relationships
        Assert.NotNull(result2.Relationships);
        var relNames2 = result2
            .Relationships.Select(r => r.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("orbits", relNames2);
    }

    [CnpgOnlyFact]
    public async Task SearchModels_VectorSimilarity_ReturnsOrderedResults()
    {
        await Client.DeleteAllModelsAsync();

        await Client.CreateModelsAsync(
            [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor, SampleData.DtdlCrater]
        );

        double[] roomEmbedding = [1.0, 0.0, 0.0];
        double[] sensorEmbedding = [0.0, 1.0, 0.0];
        double[] craterEmbedding = [0.0, 0.0, 1.0];

        var roomPatch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/embedding"), JsonSerializer.SerializeToNode(roomEmbedding))
        );
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:room;1", roomPatch);

        var sensorPatch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/embedding"), JsonSerializer.SerializeToNode(sensorEmbedding))
        );
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:tempsensor;1", sensorPatch);

        var craterPatch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/embedding"), JsonSerializer.SerializeToNode(craterEmbedding))
        );
        await Client.UpdateModelAsync("dtmi:com:contoso:Crater;1", craterPatch);

        double[] queryVector = [1.1, 0.0, 0.0];
        var results = await Client.SearchModelsAsync(
            query: null,
            vector: queryVector,
            limit: 3
        );

        var resultList = results.ToList();
        Assert.Equal(3, resultList.Count);
        Assert.Equal("dtmi:com:adt:dtsample:room;1", resultList[0].Id);
    }

    [CnpgOnlyFact]
    public async Task SearchModels_VectorSimilarityWithTextFilter_ReturnsFilteredResults()
    {
        await Client.DeleteAllModelsAsync();

        await Client.CreateModelsAsync(
            [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor, SampleData.DtdlCrater]
        );

        double[] roomEmbedding = [1.0, 0.0, 0.0];
        double[] sensorEmbedding = [0.0, 1.0, 0.0];
        double[] craterEmbedding = [0.0, 0.0, 1.0];

        var roomPatch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/embedding"), JsonSerializer.SerializeToNode(roomEmbedding))
        );
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:room;1", roomPatch);

        var sensorPatch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/embedding"), JsonSerializer.SerializeToNode(sensorEmbedding))
        );
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:tempsensor;1", sensorPatch);

        var craterPatch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/embedding"), JsonSerializer.SerializeToNode(craterEmbedding))
        );
        await Client.UpdateModelAsync("dtmi:com:contoso:Crater;1", craterPatch);

        double[] queryVector = [1.1, 0.0, 0.0];
        var results = await Client.SearchModelsAsync(
            query: "Temperature",
            vector: queryVector,
            limit: 3
        );

        var resultList = results.ToList();
        Assert.Single(resultList);
        Assert.Equal("dtmi:com:adt:dtsample:tempsensor;1", resultList[0].Id);
    }

    [CnpgOnlyFact]
    public async Task SearchModels_HnswIndex_VectorSearchWorksWithIndex()
    {
        await Client.DeleteAllModelsAsync();

        await Client.CreateModelsAsync(
            [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor, SampleData.DtdlCrater]
        );

        double[] embedding = [0.5, 0.5, 0.5];
        var patch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/embedding"), JsonSerializer.SerializeToNode(embedding))
        );
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:room;1", patch);

        double[] queryVector = [0.5, 0.5, 0.5];
        var resultsBefore = await Client.SearchModelsAsync(
            query: null,
            vector: queryVector,
            limit: 1
        );
        Assert.Single(resultsBefore);

        string graphName = Client.GetGraphName();
        await using var connection = await Client.GetDataSource().OpenConnectionAsync();

        var createIndexCmd = new NpgsqlCommand(
            $"""
            CREATE INDEX IF NOT EXISTS model_embedding_idx ON "{graphName}"."Model"
            USING hnsw ((ag_catalog.agtype_access_operator(properties, '"embedding"'::agtype)::text::vector(3)) vector_l2_ops)
            """,
            connection
        );
        await createIndexCmd.ExecuteNonQueryAsync();

        try
        {
            var resultsAfter = await Client.SearchModelsAsync(
                query: null,
                vector: queryVector,
                limit: 1
            );
            Assert.Single(resultsAfter);
            Assert.Equal("dtmi:com:adt:dtsample:room;1", resultsAfter.First().Id);
        }
        finally
        {
            await using var dropIndexCmd = new NpgsqlCommand(
                $"""
                DROP INDEX IF EXISTS "{graphName}".model_embedding_idx
                """,
                connection
            );
            await dropIndexCmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task CreateModels_ReferencedNestedSchemas_ResolveViaDtdlParser()
    {
        // Hypothesis test for konnektr-io/pg-age-digitaltwins#98.
        // DTDL requires non-interface schemas (Enum/Object) to be declared in the SAME
        // Interface's `schemas` property and referenced by DTMI. This test verifies the
        // DTDL parser resolves those references from the interface document itself — so
        // NO code change (persisting nested schemas as separate Model vertices) is needed:
        //   1) an Interface with nested referenced Enum/Object is creatable, and
        //   2) a twin whose property values satisfy the referenced schemas is accepted, and
        //   3) a twin with an invalid enum value is REJECTED (proves resolution actually ran).
        // It deliberately does NOT assert GetModelAsync on the nested schemas, since that
        // would test a separate (optional) capability, not the hypothesis.

        string interfaceModel = """
            {
              "@id": "dtmi:test:Block;1",
              "@type": "Interface",
              "@context": "dtmi:dtdl:context;4",
              "schemas": [
                {
                  "@id": "dtmi:test:BlockKind;1",
                  "@type": "Enum",
                  "valueSchema": "string",
                  "enumValues": [
                    { "name": "travel", "enumValue": "travel" },
                    { "name": "resort", "enumValue": "resort" },
                    { "name": "transit", "enumValue": "transit" }
                  ]
                },
                {
                  "@id": "dtmi:test:GeoPoint;1",
                  "@type": "Object",
                  "fields": [
                    { "name": "lat", "schema": "double" },
                    { "name": "lon", "schema": "double" }
                  ]
                }
              ],
              "contents": [
                { "@type": "Property", "name": "kind", "schema": "dtmi:test:BlockKind;1" },
                { "@type": "Property", "name": "location", "schema": "dtmi:test:GeoPoint;1" }
              ]
            }
            """;

        // Clean up any prior run
        try { await Client.DeleteModelAsync("dtmi:test:Block;1"); } catch (ModelNotFoundException) { }

        // Act 1: create the interface (with its nested schemas).
        await Client.CreateModelsAsync([interfaceModel]);

        // Assert 1: interface was created.
        Assert.NotNull(await Client.GetModelAsync("dtmi:test:Block;1"));

        // Act 2: create a twin whose values satisfy the referenced schemas.
        string twinId = "test-referenced-schema-twin";
        try { await Client.DeleteDigitalTwinAsync(twinId); } catch { }
        var validTwin = """
            {
              "$dtId": "test-referenced-schema-twin",
              "$metadata": { "model": "dtmi:test:Block;1" },
              "kind": "travel",
              "location": { "lat": 51.05, "lon": 3.72 }
            }
            """;
        await Client.CreateOrReplaceDigitalTwinAsync(twinId, validTwin);

        // Act 3: an invalid enum value must be rejected (proves the schema resolved).
        var invalidTwin = """
            {
              "$dtId": "test-referenced-schema-twin-bad",
              "$metadata": { "model": "dtmi:test:Block;1" },
              "kind": "NOT_A_BLOCK_KIND",
              "location": { "lat": 51.05, "lon": 3.72 }
            }
            """;
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => Client.CreateOrReplaceDigitalTwinAsync("test-referenced-schema-twin-bad", invalidTwin)
        );

        // Cleanup
        try { await Client.DeleteDigitalTwinAsync(twinId); } catch { }
        await Client.DeleteModelAsync("dtmi:test:Block;1");
    }
}
