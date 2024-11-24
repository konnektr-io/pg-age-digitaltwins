using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using DTDLParser;

namespace AgeDigitalTwins.Test;

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
            var resultJson = JsonDocument.Parse(results[i].DtdlModel);
            var resultId = resultJson.RootElement.GetProperty("@id").GetString();

            Assert.Equal(sampleDataId, resultId);
        }

        var result = await Client.GetModelAsync("dtmi:com:adt:dtsample:room;1");
        Assert.NotNull(result);
        var parsedResult = JsonDocument.Parse(result.DtdlModel);
        Assert.Equal(sampleDataId, parsedResult.RootElement.GetProperty("@id").GetString());
    }

    [Fact]
    public async Task CreateModels_MultipleDependentModels_ValidatedAndCreated()
    {
        string[] models = [SampleData.DtdlPlanet, SampleData.DtdlCelestialBody, SampleData.DtdlCrater];
        var results = await Client.CreateModelsAsync(models);

        for (int i = 0; i < models.Length; i++)
        {
            var resultJson = JsonDocument.Parse(results[i].DtdlModel);
            var sampleDataJson = JsonDocument.Parse(models[i]);

            var resultId = resultJson.RootElement.GetProperty("@id").GetString();
            var sampleDataId = sampleDataJson.RootElement.GetProperty("@id").GetString();

            Assert.Equal(sampleDataId, resultId);
        }
    }

    [Fact]
    public async Task CreateModels_MultipleDependentModelsResolveInDb_ValidatedAndCreated()
    {
        await Client.CreateModelsAsync([SampleData.DtdlCelestialBody, SampleData.DtdlCrater]);

        string[] models = [SampleData.DtdlPlanet];
        var results = await Client.CreateModelsAsync(models);

        for (int i = 0; i < models.Length; i++)
        {
            var resultJson = JsonDocument.Parse(results[i].DtdlModel);
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
    public async Task DeleteModels_DependentModels_DeletesEdges()
    {
        await Client.CreateModelsAsync([SampleData.DtdlCelestialBody, SampleData.DtdlCrater, SampleData.DtdlPlanet]);

        await Client.DeleteModelAsync("dtmi:com:contoso:CelestialBody;1");
    }

}
