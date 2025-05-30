using System.Text.Json.Nodes;

namespace AgeDigitalTwins.Events.Test;

[Trait("Category", "Unit")]
public class CloudEventFactoryTests
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
        var result = CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(
            eventData,
            source,
            []
        );

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
            () =>
                CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(eventData!, source, [])
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
            () => CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(eventData, source, [])
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
            () => CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(eventData, source, [])
        );
    }

    [Fact]
    public void CreateDigitalTwinChangeNotificationEvents_WithCustomTypeMapping_UsesCustomType()
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
        var mapping = new Dictionary<SinkEventType, string>
        {
            { SinkEventType.TwinUpdate, "Custom.Type.Update" },
        };

        // Act
        var result = CloudEventFactory.CreateDigitalTwinChangeNotificationEvents(
            eventData,
            source,
            mapping
        );

        // Assert
        Assert.Single(result);
        var cloudEvent = result[0];
        Assert.Equal("Custom.Type.Update", cloudEvent.Type);
    }

    [Fact]
    public void CreateEventNotificationEvents_WithTypeMapping_UsesCustomType()
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
        var mapping = new Dictionary<SinkEventType, string>
        {
            { SinkEventType.TwinUpdate, "Custom.Type.Update" },
        };

        // Act
        var result = CloudEventFactory.CreateEventNotificationEvents(eventData, source, mapping);

        // Assert
        Assert.Single(result);
        var cloudEvent = result[0];
        Assert.Equal("Custom.Type.Update", cloudEvent.Type);
    }

    [Fact]
    public void CreateDataHistoryEvents_WithTypeMapping_UsesCustomType()
    {
        // Arrange
        var eventData = new EventData
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse("{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"model1\"}}")!
                .AsObject(),
            OldValue = JsonNode
                .Parse(
                    "{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"model1\"}, \"test\": \"test\"}"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");
        var mapping = new Dictionary<SinkEventType, string>
        {
            { SinkEventType.PropertyEvent, "Custom.DataHistory.PropertyEventType" },
        };

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, mapping);

        // Assert
        Assert.Contains(result, ce => ce.Type == "Custom.DataHistory.PropertyEventType");
    }

    [Fact]
    public void CreateDataHistoryEventsModelChange_WithTypeMapping_UsesCustomType()
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
        var mapping = new Dictionary<SinkEventType, string>
        {
            { SinkEventType.TwinLifecycle, "Custom.DataHistory.TwinLifecycle" },
        };

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, mapping);

        // Assert
        Assert.Contains(result, ce => ce.Type == "Custom.DataHistory.TwinLifecycle");
    }
}
