using System.Text.Json;
using AgeDigitalTwins;
using AgeDigitalTwins.MCPServerSSE.Tools;
using Moq;
using Xunit;

public class DigitalTwinsToolsTests
{
    [Fact]
    public async Task GetDigitalTwin_ReturnsExpectedTwin()
    {
        // Arrange
        var mockClient = new Mock<AgeDigitalTwinsClient>(null, null, "testgraph");
        var twinId = "twin1";
        var expectedTwin = JsonDocument.Parse("{\"id\":\"twin1\"}");
        mockClient
            .Setup(client => client.GetDigitalTwinAsync<JsonDocument>(twinId, default))
            .ReturnsAsync(expectedTwin);

        // Act
        var result = await DigitalTwinsTools.GetDigitalTwin(mockClient.Object, twinId);

        // Assert
        Assert.Equal(expectedTwin.ToString(), result);
    }
}
