using System.Text.Json.Nodes;

namespace AgeDigitalTwins.Events.Test;

public class CloudEventFactoryTest
{
    [Fact]
    public void CreateDigitalTwinChangeNotificationEvents_ValidEventData_ReturnsCloudEvent()
    {
        // Arrange
        var eventData = new EventData
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse("{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"model1\"}}")!
                .AsObject(),
            OldValue = JsonNode
                .Parse("{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"model0\"}}")!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(eventData, source);

        // Assert
        Assert.Single(result);
        var cloudEvent = result[0];
        Assert.Equal("Konnektr.DigitalTwins.Twin.Update", cloudEvent.Type);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.Equal("twin1", cloudEvent.Subject);
        Assert.Equal(source, cloudEvent.Source);
        Assert.Equal(eventData.Timestamp, cloudEvent.Time);
        var data = cloudEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("model1", data["modelId"]?.ToString());
        Assert.NotNull(data["patch"]);
    }

    [Fact]
    public void CreateDigitalTwinChangeNotificationEvents_NullEventData_ThrowsArgumentNullException()
    {
        // Arrange
        EventData? eventData = null;
        var source = new Uri("http://example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(eventData!, source)
        );
    }

    [Fact]
    public void CreateDigitalTwinChangeNotificationEvents_InvalidEventType_ThrowsArgumentNullException()
    {
        // Arrange
        var eventData = new EventData
        {
            EventType = EventType.TwinCreate,
            NewValue = JsonNode.Parse("{\"$dtId\": \"twin1\"}")!.AsObject(),
            OldValue = JsonNode.Parse("{\"$dtId\": \"twin1\"}")!.AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(eventData, source)
        );
    }

    [Fact]
    public void CreateDigitalTwinChangeNotificationEvents_MissingDtId_ThrowsArgumentException()
    {
        // Arrange
        var eventData = new EventData
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode.Parse("{\"$metadata\": {\"$model\": \"model1\"}}")!.AsObject(),
            OldValue = JsonNode
                .Parse("{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"model0\"}}")!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(eventData, source)
        );
    }
}
