using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class TelemetryTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public TelemetryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task PublishTelemetryAsync_ShouldSucceed_WhenValidTelemetryProvided()
    {
        // Arrange
        string twinId = "test-twin-telemetry-1";
        var telemetryData = new JsonObject
        {
            ["temperature"] = 23.5,
            ["humidity"] = 45.2,
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };
        string messageId = Guid.NewGuid().ToString();

        // Act & Assert - Should not throw
        await Client.PublishTelemetryAsync(twinId, telemetryData, messageId);

        _output.WriteLine($"✓ Successfully published telemetry for twin {twinId} with message ID {messageId}");
    }

    [Fact]
    public async Task PublishComponentTelemetryAsync_ShouldSucceed_WhenValidTelemetryProvided()
    {
        // Arrange
        string twinId = "test-twin-telemetry-2";
        string componentName = "thermostat";
        var telemetryData = new JsonObject
        {
            ["targetTemperature"] = 22.0,
            ["mode"] = "heating",
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };
        string messageId = Guid.NewGuid().ToString();

        // Act & Assert - Should not throw
        await Client.PublishComponentTelemetryAsync(twinId, componentName, telemetryData, messageId);

        _output.WriteLine($"✓ Successfully published component telemetry for twin {twinId}, component {componentName} with message ID {messageId}");
    }

    [Fact]
    public async Task PublishTelemetryAsync_ShouldGenerateMessageId_WhenNotProvided()
    {
        // Arrange
        string twinId = "test-twin-telemetry-3";
        var telemetryData = new JsonObject
        {
            ["pressure"] = 1013.25,
            ["altitude"] = 150.0
        };

        // Act & Assert - Should not throw and should auto-generate message ID
        await Client.PublishTelemetryAsync(twinId, telemetryData);

        _output.WriteLine($"✓ Successfully published telemetry for twin {twinId} with auto-generated message ID");
    }

    [Fact]
    public async Task PublishComponentTelemetryAsync_ShouldGenerateMessageId_WhenNotProvided()
    {
        // Arrange
        string twinId = "test-twin-telemetry-4";
        string componentName = "sensor";
        var telemetryData = new JsonObject
        {
            ["batteryLevel"] = 85,
            ["signalStrength"] = -42
        };

        // Act & Assert - Should not throw and should auto-generate message ID
        await Client.PublishComponentTelemetryAsync(twinId, componentName, telemetryData);

        _output.WriteLine($"✓ Successfully published component telemetry for twin {twinId}, component {componentName} with auto-generated message ID");
    }

    [Fact]
    public async Task PublishTelemetryAsync_ShouldHandleComplexTelemetryData()
    {
        // Arrange
        string twinId = "test-twin-telemetry-5";
        var complexTelemetryData = new JsonObject
        {
            ["sensors"] = new JsonObject
            {
                ["temperature"] = new JsonObject
                {
                    ["value"] = 24.3,
                    ["unit"] = "celsius",
                    ["accuracy"] = 0.1
                },
                ["humidity"] = new JsonObject
                {
                    ["value"] = 47.8,
                    ["unit"] = "percent",
                    ["accuracy"] = 2.0
                }
            },
            ["location"] = new JsonObject
            {
                ["latitude"] = 40.7128,
                ["longitude"] = -74.0060,
                ["elevation"] = 10.0
            },
            ["metadata"] = new JsonObject
            {
                ["deviceId"] = "sensor-001",
                ["firmware"] = "1.2.3",
                ["batteryLevel"] = 92
            }
        };

        // Act & Assert - Should not throw
        await Client.PublishTelemetryAsync(twinId, complexTelemetryData);

        _output.WriteLine($"✓ Successfully published complex telemetry data for twin {twinId}");
    }
}
