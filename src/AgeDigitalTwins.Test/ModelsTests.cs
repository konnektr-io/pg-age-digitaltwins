using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using DTDLParser;

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
}
