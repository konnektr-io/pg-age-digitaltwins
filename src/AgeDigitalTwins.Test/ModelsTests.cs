using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using DTDLParser;
using Npgsql.Age;

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

    [Fact]
    public async Task UpdateModel_Decommission_SetsDecommissionedFlag()
    {
        // Arrange
        try
        {
            await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
        }
        catch (ModelNotFoundException)
        {
            // Ignore if model doesn't exist
        }

        await Client.CreateModelsAsync([SampleData.DtdlRoom]);

        // Act: Decommission the model
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:room;1", decommissioned: true);

        // Assert
        var model = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.True(model.IsDecommissioned);

        // Act: Recommission the model
        await Client.UpdateModelAsync("dtmi:com:adt:dtsample:room;1", decommissioned: false);

        // Assert
        model = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.False(model.IsDecommissioned);

        // Cleanup
        await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
    }

    [Fact]
    public async Task UpdateModel_NonExistentModel_ThrowsModelNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ModelNotFoundException>(async () =>
        {
            await Client.UpdateModelAsync("dtmi:com:nonexistent:model;1", decommissioned: true);
        });
    }

    [Fact]
    public async Task ReplaceModel_AddProperty_SucceedsWhenNoConflict()
    {
        // Arrange: Clean up and create a model without descendants
        try
        {
            await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
        }
        catch (ModelNotFoundException)
        {
            // Ignore if model doesn't exist
        }

        await Client.CreateModelsAsync([SampleData.DtdlRoom]);

        // Create an updated version with an additional property
        string updatedModel =
            @"{
            ""@id"": ""dtmi:com:adt:dtsample:room;1"",
            ""@type"": ""Interface"",
            ""@context"": [
                ""dtmi:dtdl:context;3"",
                ""dtmi:dtdl:extension:quantitativeTypes;1""
            ],
            ""displayName"": ""Room"",
            ""contents"": [
                {
                    ""@type"": ""Property"",
                    ""name"": ""name"",
                    ""schema"": ""string""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""description"",
                    ""schema"": ""string""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""temperature"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": [""Property"", ""Humidity""],
                    ""name"": ""humidity"",
                    ""schema"": ""double"",
                    ""unit"": ""gramPerCubicMetre""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""dimensions"",
                    ""schema"": {
                        ""@type"": ""Object"",
                        ""fields"": [
                            { ""name"": ""length"", ""schema"": ""double"" },
                            { ""name"": ""width"", ""schema"": ""double"" },
                            { ""name"": ""height"", ""schema"": ""double"" }
                        ]
                    }
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""capacity"",
                    ""schema"": ""integer""
                },
                {
                    ""@type"": ""Relationship"",
                    ""@id"": ""dtmi:com:adt:dtsample:room:rel_has_sensors;1"",
                    ""name"": ""rel_has_sensors"",
                    ""displayName"": ""Room has sensors""
                }
            ]
        }";

        // Act
        var result = await Client.CreateOrReplaceModelAsync(
            "dtmi:com:adt:dtsample:room;1",
            updatedModel
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dtmi:com:adt:dtsample:room;1", result.Id);
        Assert.NotNull(result.DtdlModel);

        // Verify the new property is present
        var modelJson = JsonDocument.Parse(result.DtdlModel!);
        var contents = modelJson.RootElement.GetProperty("contents");
        var propertyNames = contents
            .EnumerateArray()
            .Where(c =>
                c.TryGetProperty("@type", out var type)
                && (
                    type.ValueKind == JsonValueKind.String && type.GetString() == "Property"
                    || type.ValueKind == JsonValueKind.Array
                        && type.EnumerateArray().Any(t => t.GetString() == "Property")
                )
            )
            .Select(c => c.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("capacity", propertyNames);

        // Cleanup
        await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
    }

    [Fact]
    public async Task ReplaceModel_ChangeModelId_ThrowsModelUpdateValidationException()
    {
        // Arrange
        try
        {
            await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
        }
        catch (ModelNotFoundException)
        {
            // Ignore if model doesn't exist
        }

        await Client.CreateModelsAsync([SampleData.DtdlRoom]);

        // Try to replace with a different model ID
        string differentIdModel =
            @"{
            ""@id"": ""dtmi:com:adt:dtsample:different;1"",
            ""@type"": ""Interface"",
            ""@context"": ""dtmi:dtdl:context;3"",
            ""displayName"": ""Different"",
            ""contents"": []
        }";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelUpdateValidationException>(async () =>
        {
            await Client.CreateOrReplaceModelAsync(
                "dtmi:com:adt:dtsample:room;1",
                differentIdModel
            );
        });
        Assert.Contains("Model ID cannot be changed", ex.Message);

        // Cleanup
        await Client.DeleteModelAsync("dtmi:com:adt:dtsample:room;1");
    }

    [Fact]
    public async Task ReplaceModel_ChangeExtends_ThrowsModelExtendsChangedException()
    {
        // Arrange: Create base models and a derived model
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
            catch (ModelNotFoundException)
            {
                // Ignore if model doesn't exist
            }
        }

        await Client.CreateModelsAsync(
            [SampleData.DtdlCelestialBody, SampleData.DtdlCrater, SampleData.DtdlPlanet]
        );

        // Try to replace Planet with a version that doesn't extend CelestialBody
        string planetNoExtends =
            @"{
            ""@context"": ""dtmi:dtdl:context;3"",
            ""@id"": ""dtmi:com:contoso:Planet;1"",
            ""@type"": ""Interface"",
            ""displayName"": ""Planet"",
            ""contents"": [
                {
                    ""@type"": ""Property"",
                    ""name"": ""hasLife"",
                    ""schema"": ""boolean""
                }
            ]
        }";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelExtendsChangedException>(async () =>
        {
            await Client.CreateOrReplaceModelAsync("dtmi:com:contoso:Planet;1", planetNoExtends);
        });
        Assert.Contains("Changing what a model extends is not supported", ex.Message);
    }

    [Fact]
    public async Task ReplaceModel_AddPropertyConflictingWithDescendant_ThrowsModelUpdateValidationException()
    {
        // Arrange: Create a hierarchy where we'll add a conflicting property to the base
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

        await Client.CreateModelsAsync(
            [
                SampleData.DtdlCelestialBody,
                SampleData.DtdlCrater,
                SampleData.DtdlPlanet,
                SampleData.DtdlHabitablePlanet,
            ]
        );

        // Try to add a property 'hasLife' to CelestialBody - which already exists in Planet (a descendant)
        string celestialBodyWithConflict =
            @"{
            ""@context"": ""dtmi:dtdl:context;3"",
            ""@id"": ""dtmi:com:contoso:CelestialBody;1"",
            ""@type"": ""Interface"",
            ""displayName"": ""Celestial body"",
            ""contents"": [
                {
                    ""@type"": ""Property"",
                    ""name"": ""name"",
                    ""schema"": ""string""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""mass"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""temperature"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""hasLife"",
                    ""schema"": ""boolean""
                },
                {
                    ""@type"": ""Relationship"",
                    ""name"": ""orbits"",
                    ""target"": ""dtmi:com:contoso:CelestialBody;1""
                }
            ]
        }";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelUpdateValidationException>(async () =>
        {
            await Client.CreateOrReplaceModelAsync(
                "dtmi:com:contoso:CelestialBody;1",
                celestialBodyWithConflict
            );
        });
        Assert.Contains("conflicts with descendant models", ex.Message);
    }

    [Fact]
    public async Task ReplaceModel_NonExistentModel_ThrowsModelNotFoundException()
    {
        // Act & Assert
        string model =
            @"{
            ""@id"": ""dtmi:com:nonexistent:model;1"",
            ""@type"": ""Interface"",
            ""@context"": ""dtmi:dtdl:context;3"",
            ""displayName"": ""Nonexistent"",
            ""contents"": []
        }";

        await Assert.ThrowsAsync<ModelNotFoundException>(async () =>
        {
            await Client.CreateOrReplaceModelAsync("dtmi:com:nonexistent:model;1", model);
        });
    }

    [Fact]
    public async Task ReplaceModel_AddPropertyToBaseModel_SucceedsWhenNoConflict()
    {
        // Arrange: Create base and derived models
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
            catch (ModelNotFoundException)
            {
                // Ignore if model doesn't exist
            }
        }

        await Client.CreateModelsAsync(
            [SampleData.DtdlCelestialBody, SampleData.DtdlCrater, SampleData.DtdlPlanet]
        );

        // Add a new property 'age' to CelestialBody (no conflict with descendants)
        string celestialBodyUpdated =
            @"{
            ""@context"": ""dtmi:dtdl:context;3"",
            ""@id"": ""dtmi:com:contoso:CelestialBody;1"",
            ""@type"": ""Interface"",
            ""displayName"": ""Celestial body"",
            ""contents"": [
                {
                    ""@type"": ""Property"",
                    ""name"": ""name"",
                    ""schema"": ""string""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""mass"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""temperature"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""age"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Relationship"",
                    ""name"": ""orbits"",
                    ""target"": ""dtmi:com:contoso:CelestialBody;1""
                }
            ]
        }";

        // Act
        var result = await Client.CreateOrReplaceModelAsync(
            "dtmi:com:contoso:CelestialBody;1",
            celestialBodyUpdated
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dtmi:com:contoso:CelestialBody;1", result.Id);

        // Verify descendants are preserved
        Assert.NotNull(result.Descendants);
        Assert.Contains("dtmi:com:contoso:Planet;1", result.Descendants);

        // Verify the new property is present
        var modelJson = JsonDocument.Parse(result.DtdlModel!);
        var contents = modelJson.RootElement.GetProperty("contents");
        var propertyNames = contents
            .EnumerateArray()
            .Where(c => c.TryGetProperty("@type", out var type) && type.GetString() == "Property")
            .Select(c => c.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("age", propertyNames);
    }

    [Fact]
    public async Task ReplaceModel_AddComponent_CreatesHasComponentRelationship()
    {
        // Arrange: Create base models
        string[] modelIds = ["dtmi:com:contoso:CelestialBody;1", "dtmi:com:contoso:Crater;1"];
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

        await Client.CreateModelsAsync([SampleData.DtdlCelestialBody, SampleData.DtdlCrater]);

        // Create CelestialBody without components
        string celestialBodyNoComponent =
            @"{
            ""@context"": ""dtmi:dtdl:context;3"",
            ""@id"": ""dtmi:com:contoso:CelestialBody;1"",
            ""@type"": ""Interface"",
            ""displayName"": ""Celestial body"",
            ""contents"": [
                {
                    ""@type"": ""Property"",
                    ""name"": ""name"",
                    ""schema"": ""string""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""mass"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""temperature"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Relationship"",
                    ""name"": ""orbits"",
                    ""target"": ""dtmi:com:contoso:CelestialBody;1""
                }
            ]
        }";

        // First replace to version without component
        await Client.CreateOrReplaceModelAsync(
            "dtmi:com:contoso:CelestialBody;1",
            celestialBodyNoComponent
        );

        // Now add a component
        string celestialBodyWithComponent =
            @"{
            ""@context"": ""dtmi:dtdl:context;3"",
            ""@id"": ""dtmi:com:contoso:CelestialBody;1"",
            ""@type"": ""Interface"",
            ""displayName"": ""Celestial body"",
            ""contents"": [
                {
                    ""@type"": ""Property"",
                    ""name"": ""name"",
                    ""schema"": ""string""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""mass"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""temperature"",
                    ""schema"": ""double""
                },
                {
                    ""@type"": ""Component"",
                    ""name"": ""mainCrater"",
                    ""schema"": ""dtmi:com:contoso:Crater;1""
                },
                {
                    ""@type"": ""Relationship"",
                    ""name"": ""orbits"",
                    ""target"": ""dtmi:com:contoso:CelestialBody;1""
                }
            ]
        }";

        // Act
        var result = await Client.CreateOrReplaceModelAsync(
            "dtmi:com:contoso:CelestialBody;1",
            celestialBodyWithComponent
        );

        // Assert: Verify the component is in the model
        Assert.NotNull(result);
        var modelJson = JsonDocument.Parse(result.DtdlModel!);
        var contents = modelJson.RootElement.GetProperty("contents");
        var components = contents
            .EnumerateArray()
            .Where(c => c.TryGetProperty("@type", out var type) && type.GetString() == "Component")
            .ToList();
        Assert.Single(components);
        Assert.Equal("mainCrater", components[0].GetProperty("name").GetString());

        // Verify the _hasComponent relationship was created in the graph
        var graphName = Client.GetGraphName();
        var dataSource = Client.GetDataSource();
        await using var connection = await dataSource.OpenConnectionAsync();
        string cypher =
            $@"
            MATCH (m:Model {{id: 'dtmi:com:contoso:CelestialBody;1'}})
                  -[:_hasComponent]->
                  (m2:Model {{id: 'dtmi:com:contoso:Crater;1'}})
            RETURN COUNT(*) as count";
        await using var command = connection.CreateCypherCommand(graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var agResult = await reader.GetFieldValueAsync<Npgsql.Age.Types.Agtype>(0);
        int count = (int)agResult;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ReplaceModel_RemoveComponent_DeletesHasComponentRelationship()
    {
        // Arrange: Create models with component
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
            catch (ModelNotFoundException)
            {
                // Ignore if model doesn't exist
            }
        }

        await Client.CreateModelsAsync(
            [SampleData.DtdlCelestialBody, SampleData.DtdlCrater, SampleData.DtdlPlanet]
        );

        // Planet originally has a deepestCrater component
        // Now replace it without the component
        string planetNoComponent =
            @"{
            ""@context"": ""dtmi:dtdl:context;3"",
            ""@id"": ""dtmi:com:contoso:Planet;1"",
            ""@type"": ""Interface"",
            ""displayName"": ""Planet"",
            ""extends"": ""dtmi:com:contoso:CelestialBody;1"",
            ""contents"": [
                {
                    ""@type"": ""Relationship"",
                    ""name"": ""satellites"",
                    ""target"": ""dtmi:com:contoso:Moon;1""
                },
                {
                    ""@type"": ""Property"",
                    ""name"": ""hasLife"",
                    ""schema"": ""boolean""
                }
            ]
        }";

        // Act
        var result = await Client.CreateOrReplaceModelAsync(
            "dtmi:com:contoso:Planet;1",
            planetNoComponent
        );

        // Assert: Verify the component is removed from the model
        Assert.NotNull(result);
        var modelJson = JsonDocument.Parse(result.DtdlModel!);
        var contents = modelJson.RootElement.GetProperty("contents");
        var components = contents
            .EnumerateArray()
            .Where(c => c.TryGetProperty("@type", out var type) && type.GetString() == "Component")
            .ToList();
        Assert.Empty(components);

        // Verify the _hasComponent relationship was removed from the graph
        var graphName = Client.GetGraphName();
        var dataSource = Client.GetDataSource();
        await using var connection = await dataSource.OpenConnectionAsync();
        string cypher =
            $@"
            MATCH (m:Model {{id: 'dtmi:com:contoso:Planet;1'}})
                  -[:_hasComponent]->
                  (m2:Model {{id: 'dtmi:com:contoso:Crater;1'}})
            RETURN COUNT(*) as count";
        await using var command = connection.CreateCypherCommand(graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var agResult = await reader.GetFieldValueAsync<Npgsql.Age.Types.Agtype>(0);
        int count = (int)agResult;
        Assert.Equal(0, count);
    }
}
