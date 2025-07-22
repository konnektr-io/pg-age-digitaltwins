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
        var twinLifeCycleEvent = result.FirstOrDefault(ce =>
            ce.Type == "Custom.DataHistory.TwinLifecycle"
        );
        Assert.NotNull(twinLifeCycleEvent);
        Assert.Equal("application/json", twinLifeCycleEvent.DataContentType);
        Assert.Equal("twin1", twinLifeCycleEvent.Subject);
        Assert.Equal(source, twinLifeCycleEvent.Source);
        var data = twinLifeCycleEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("model1", data["modelId"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_HandlesTwinDeleteEvent()
    {
        // Arrange
        var eventData = new EventData
        {
            GraphName = "digitaltwins",
            TableName = "twin",
            EventType = EventType.TwinDelete,
            NewValue = null,
            OldValue = JsonNode
                .Parse("{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"model1\"}}")!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert
        Assert.Single(result);
        var cloudEvent = result[0];
        Assert.Equal("Konnektr.DigitalTwins.Twin.Lifecycle", cloudEvent.Type);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.Equal("twin1", cloudEvent.Subject);
        Assert.Equal(source, cloudEvent.Source);
    }

    [Fact]
    public void CreateDataHistoryEvents_HandlesTwinDeleteEventWithProperties()
    {
        // Arrange
        var eventData = new EventData
        {
            GraphName = "digitaltwins",
            TableName = "twin",
            EventType = EventType.TwinDelete,
            NewValue = null,
            OldValue = JsonNode
                .Parse(
                    "{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"model1\"}, \"test\": 123}"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert
        Assert.Single(result);
        var cloudEvent = result[0];
        Assert.Equal("Konnektr.DigitalTwins.Twin.Lifecycle", cloudEvent.Type);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.Equal("twin1", cloudEvent.Subject);
        Assert.Equal(source, cloudEvent.Source);
    }

    [Fact]
    public void CreateDataHistoryEvents_HandlesTwinCreateEventWithProperties()
    {
        // Arrange
        var eventData = new EventData
        {
            GraphName = "digitaltwins",
            TableName = "twin",
            EventType = EventType.TwinCreate,
            NewValue = JsonNode
                .Parse(
                    "{\"$dtId\": \"twin1\", \"$metadata\": {\"$model\": \"model1\"}, \"test\": 123}"
                )!
                .AsObject(),
            OldValue = new JsonObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert
        Assert.Equal(2, result.Count);
        var lifecycleEvent = result[0];
        Assert.Equal("Konnektr.DigitalTwins.Twin.Lifecycle", lifecycleEvent.Type);
        Assert.Equal("application/json", lifecycleEvent.DataContentType);
        Assert.Equal("twin1", lifecycleEvent.Subject);
        Assert.Equal(source, lifecycleEvent.Source);
        var propertyEvent = result[1];
        Assert.Equal("Konnektr.DigitalTwins.Property.Event", propertyEvent.Type);
        Assert.Equal("application/json", propertyEvent.DataContentType);
        Assert.Equal("twin1", propertyEvent.Subject);
        var propertyEventData = propertyEvent.Data as JsonObject;
        Assert.NotNull(propertyEventData);
        Assert.Equal("twin1", propertyEventData["id"]?.ToString());
        Assert.Equal("model1", propertyEventData["modelId"]?.ToString());
        Assert.Equal("test", propertyEventData["key"]?.ToString());
        Assert.Equal("123", propertyEventData["value"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_HandlesRelationshipCreateEvent()
    {
        // Arrange
        var eventData = new EventData
        {
            GraphName = "digitaltwins",
            TableName = "has",
            EventType = EventType.RelationshipCreate,
            NewValue = JsonNode
                .Parse(
                    "{\"$relationshipId\": \"rel1\", \"$sourceId\": \"twin1\", \"$targetId\": \"twin2\", \"$relationshipName\": \"has\"}"
                )!
                .AsObject(),
            OldValue = new JsonObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert
        Assert.Single(result);
        var cloudEvent = result[0];
        Assert.Equal("Konnektr.DigitalTwins.Relationship.Lifecycle", cloudEvent.Type);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.Equal("twin1/relationships/rel1", cloudEvent.Subject);
        Assert.Equal(source, cloudEvent.Source);
        var data = cloudEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("twin1", data["source"]?.ToString());
        Assert.Equal("twin2", data["target"]?.ToString());
        Assert.Equal("has", data["name"]?.ToString());
        Assert.Equal("rel1", data["relationshipId"]?.ToString());
        Assert.Equal("Create", data["action"]?.ToString());
    }

    [Fact]
    public void CreateDigitalTwinChangeNotificationEvents_SameValueUpdate_IncludesPropertyInPatch()
    {
        // Arrange - Property 'temperature' updated with same value, but lastUpdateTime changed
        var eventData = new EventData
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z""
                        }
                    },
                    ""temperature"": 25.5
                }"
                )!
                .AsObject(),
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:00:00Z""
                        }
                    },
                    ""temperature"": 25.5
                }"
                )!
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
        var data = cloudEvent.Data as JsonObject;
        Assert.NotNull(data);

        // Verify patch contains the same-value update
        var patch = data["patch"] as JsonArray;
        Assert.NotNull(patch);
        Assert.Contains(
            patch,
            op =>
            {
                var opObj = op as JsonObject;
                return opObj != null
                    && opObj["op"]?.ToString() == "replace"
                    && opObj["path"]?.ToString() == "/temperature"
                    && opObj["value"]?.ToString() == "25.5";
            }
        );
        Assert.Contains(
            patch,
            op =>
            {
                var opObj = op as JsonObject;
                return opObj != null
                    && opObj["op"]?.ToString() == "replace"
                    && opObj["path"]?.ToString() == "/$metadata/temperature/lastUpdateTime"
                    && opObj["value"]?.ToString() == "2024-01-15T10:30:00Z";
            }
        );
    }

    [Fact]
    public void CreateDataHistoryEvents_SameValueUpdate_CreatesPropertyEvent()
    {
        // Arrange - Property 'humidity' updated with same value, but lastUpdateTime changed
        var eventData = new EventData
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""humidity"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z""
                        }
                    },
                    ""humidity"": 60.0
                }"
                )!
                .AsObject(),
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""humidity"": {
                            ""lastUpdateTime"": ""2024-01-15T10:00:00Z""
                        }
                    },
                    ""humidity"": 60.0
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert
        Assert.Single(result); // Should have 1 property event for the same-value update
        var propertyEvent = result[0];
        Assert.Equal("Konnektr.DigitalTwins.Property.Event", propertyEvent.Type);
        Assert.Equal("twin1", propertyEvent.Subject);

        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("twin1", data["id"]?.ToString());
        Assert.Equal("humidity", data["key"]?.ToString());
        Assert.Equal("60.0", data["value"]?.ToString());
        Assert.Equal("Update", data["action"]?.ToString());
    }
}
