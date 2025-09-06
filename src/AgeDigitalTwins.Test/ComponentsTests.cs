using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using Json.Patch;
using Json.Pointer;
using Xunit;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class ComponentsTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public ComponentsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetComponentAsync_ShouldReturnComponent_WhenComponentExists()
    {
        // Arrange
        string modelId = "dtmi:example:TestDevice;1";
        string dtdlModel = """
            {
              "@id": "dtmi:example:TestDevice;1",
              "@type": "Interface",
              "@context": "dtmi:dtdl:context;3",
              "contents": [
                {
                  "@type": "Component",
                  "name": "thermostat",
                  "schema": "dtmi:example:Thermostat;1"
                }
              ]
            }
            """;

        string thermostatDtdlModel = """
            {
              "@id": "dtmi:example:Thermostat;1",
              "@type": "Interface",
              "@context": "dtmi:dtdl:context;3",
              "contents": [
                {
                  "@type": "Property",
                  "name": "temperature",
                  "schema": "double"
                },
                {
                  "@type": "Property",
                  "name": "targetTemperature",
                  "schema": "double"
                }
              ]
            }
            """;

        string twinId = "test-twin-components-1";

        // Create the models
        await Client.CreateModelsAsync([dtdlModel, thermostatDtdlModel]);

        // Create a digital twin with a component
        string digitalTwinJson = $$"""
            {
              "$dtId": "{{twinId}}",
              "$metadata": { "$model": "{{modelId}}" },
              "thermostat": {
                "temperature": 23.5,
                "targetTemperature": 20.0,
                "$metadata": {
                  "$lastUpdateTime": "{{DateTime.UtcNow:o}}"
                }
              }
            }
            """;

        await Client.CreateOrReplaceDigitalTwinAsync(twinId, digitalTwinJson);

        // Act - Get component without model validation for unit testing
        var component = await Client.GetComponentAsync<JsonObject>(
            twinId,
            "thermostat",
            validateModel: false
        );

        // Assert
        Assert.NotNull(component);
        Assert.True(component.TryGetPropertyValue("temperature", out var tempNode));
        Assert.Equal(23.5, tempNode?.GetValue<double>());
        Assert.True(component.TryGetPropertyValue("targetTemperature", out var targetTempNode));
        Assert.Equal(20.0, targetTempNode?.GetValue<double>());

        _output.WriteLine($"✓ Successfully retrieved component from twin {twinId}");
    }

    [Fact]
    public async Task GetComponentAsync_ShouldThrowException_WhenDigitalTwinNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<DigitalTwinNotFoundException>(
            () =>
                Client.GetComponentAsync<JsonObject>(
                    "nonexistent-twin",
                    "thermostat",
                    validateModel: false
                )
        );

        _output.WriteLine("✓ Digital twin not found exception thrown as expected");
    }

    [Fact]
    public async Task GetComponentAsync_ShouldThrowException_WhenComponentDoesNotExist()
    {
        // Arrange
        string modelId = "dtmi:example:TestDevice;1";
        string dtdlModel = """
            {
              "@id": "dtmi:example:TestDevice;1",
              "@type": "Interface",
              "@context": "dtmi:dtdl:context;3",
              "contents": [
                {
                  "@type": "Property",
                  "name": "simpleProperty",
                  "schema": "string"
                }
              ]
            }
            """;

        string twinId = "test-twin-components-2";

        // Create the model
        await Client.CreateModelsAsync([dtdlModel]);

        // Create a digital twin without components
        string digitalTwinJson = $$"""
            {
              "$dtId": "{{twinId}}",
              "$metadata": { "$model": "{{modelId}}" },
              "simpleProperty": "test"
            }
            """;

        await Client.CreateOrReplaceDigitalTwinAsync(twinId, digitalTwinJson);

        // Act & Assert
        await Assert.ThrowsAsync<ComponentNotFoundException>(
            () =>
                Client.GetComponentAsync<JsonObject>(
                    twinId,
                    "nonExistentComponent",
                    validateModel: false
                )
        );

        _output.WriteLine($"✓ Component not found exception thrown as expected");
    }

    [Fact]
    public async Task UpdateComponentAsync_ShouldUpdateComponent_WhenValidPatchProvided()
    {
        // Arrange
        string modelId = "dtmi:example:TestDevice;1";
        string dtdlModel = """
            {
              "@id": "dtmi:example:TestDevice;1",
              "@type": "Interface",
              "@context": "dtmi:dtdl:context;3",
              "contents": [
                {
                  "@type": "Component",
                  "name": "thermostat",
                  "schema": "dtmi:example:Thermostat;1"
                }
              ]
            }
            """;

        string thermostatDtdlModel = """
            {
              "@id": "dtmi:example:Thermostat;1",
              "@type": "Interface",
              "@context": "dtmi:dtdl:context;3",
              "contents": [
                {
                  "@type": "Property",
                  "name": "temperature",
                  "schema": "double"
                },
                {
                  "@type": "Property",
                  "name": "targetTemperature",
                  "schema": "double"
                }
              ]
            }
            """;

        string twinId = "test-twin-components-3";

        // Create the models
        await Client.CreateModelsAsync([dtdlModel, thermostatDtdlModel]);

        // Create a digital twin with a component
        string digitalTwinJson = $$"""
            {
              "$dtId": "{{twinId}}",
              "$metadata": { "$model": "{{modelId}}" },
              "thermostat": {
                "temperature": 23.5,
                "targetTemperature": 20.0,
                "$metadata": {
                  "$lastUpdateTime": "{{DateTime.UtcNow:o}}"
                }
              }
            }
            """;

        await Client.CreateOrReplaceDigitalTwinAsync(twinId, digitalTwinJson);

        // Act - Update the component
        var patch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/targetTemperature"), 22.0)
        );
        await Client.UpdateComponentAsync(twinId, "thermostat", patch);

        // Assert - Verify the component was updated
        // Verify the update was successful
        var updatedComponent = await Client.GetComponentAsync<JsonObject>(
            twinId,
            "thermostat",
            validateModel: false
        );
        Assert.NotNull(updatedComponent);
        Assert.True(
            updatedComponent.TryGetPropertyValue("targetTemperature", out var targetTempNode)
        );
        Assert.Equal(22.0, targetTempNode?.GetValue<double>());

        // Verify the original temperature wasn't changed
        Assert.True(updatedComponent.TryGetPropertyValue("temperature", out var tempNode));
        Assert.Equal(23.5, tempNode?.GetValue<double>());

        _output.WriteLine($"✓ Successfully updated component on twin {twinId}");
    }

    [Fact]
    public async Task UpdateComponentAsync_ShouldThrowException_WhenDigitalTwinNotFound()
    {
        // Act & Assert
        var patch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/targetTemperature"), 22.0)
        );
        await Assert.ThrowsAsync<DigitalTwinNotFoundException>(
            () => Client.UpdateComponentAsync("nonexistent-twin", "thermostat", patch)
        );

        _output.WriteLine("✓ Digital twin not found exception thrown as expected during update");
    }

    [Fact]
    public async Task UpdateComponentAsync_ShouldThrowException_WhenComponentDoesNotExist()
    {
        // Arrange
        string modelId = "dtmi:example:TestDevice;1";
        string dtdlModel = """
            {
              "@id": "dtmi:example:TestDevice;1",
              "@type": "Interface",
              "@context": "dtmi:dtdl:context;3",
              "contents": [
                {
                  "@type": "Property",
                  "name": "simpleProperty",
                  "schema": "string"
                }
              ]
            }
            """;

        string twinId = "test-twin-components-4";

        // Create the model
        await Client.CreateModelsAsync([dtdlModel]);

        // Create a digital twin without components
        string digitalTwinJson = $$"""
            {
              "$dtId": "{{twinId}}",
              "$metadata": { "$model": "{{modelId}}" },
              "simpleProperty": "test"
            }
            """;

        await Client.CreateOrReplaceDigitalTwinAsync(twinId, digitalTwinJson);

        // Act & Assert
        var patch = new JsonPatch(
            PatchOperation.Replace(JsonPointer.Parse("/someProperty"), "newValue")
        );
        await Assert.ThrowsAsync<ComponentNotFoundException>(
            () => Client.UpdateComponentAsync(twinId, "nonExistentComponent", patch)
        );

        _output.WriteLine($"✓ Component not found exception thrown as expected during update");
    }
}
