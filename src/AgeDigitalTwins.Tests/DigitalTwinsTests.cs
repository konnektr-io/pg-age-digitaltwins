using System.Text.Json;
using AgeDigitalTwins.Exceptions;
using Azure.DigitalTwins.Core;
using DTDLParser;

namespace AgeDigitalTwins.Tests;

public class DigitalTwinsTests : TestBase
{
    [Fact]
    public async Task CreateOrReplaceDigitalTwinAsync_BasicDigitalTwin_Created()
    {
        // Load required models
        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        // Create digital twin
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        var createdTwin = await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin!.Id, digitalTwin);

        Assert.NotNull(createdTwin);
        Assert.Equal(digitalTwin.Id, createdTwin.Id);
    }
}
