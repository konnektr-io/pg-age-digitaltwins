using System.Data.Common;
using System.Text.Json.Nodes;
using AgeDigitalTwins.Events.Abstractions;
using AgeDigitalTwins.Events.Core.Events;

namespace AgeDigitalTwins.Events.Test;

[Trait("Category", "Unit")]
public class CloudEventFactoryTests
{
    [Fact]
    public void CreateDigitalTwinChangeNotificationEvents_ValidEventData_ReturnsCloudEvent()
    {
        // Arrange
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
        Assert.Equal("Konnektr.Graph.Twin.Update", cloudEvent.Type);
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
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
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
        Assert.Equal("Konnektr.Graph.Twin.Lifecycle", cloudEvent.Type);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.Equal("twin1", cloudEvent.Subject);
        Assert.Equal(source, cloudEvent.Source);
    }

    [Fact]
    public void CreateDataHistoryEvents_HandlesTwinDeleteEventWithProperties()
    {
        // Arrange
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
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
        Assert.Equal("Konnektr.Graph.Twin.Lifecycle", cloudEvent.Type);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.Equal("twin1", cloudEvent.Subject);
        Assert.Equal(source, cloudEvent.Source);
    }

    [Fact]
    public void CreateDataHistoryEvents_HandlesTwinCreateEventWithProperties()
    {
        // Arrange
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
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
        Assert.Equal("Konnektr.Graph.Twin.Lifecycle", lifecycleEvent.Type);
        Assert.Equal("application/json", lifecycleEvent.DataContentType);
        Assert.Equal("twin1", lifecycleEvent.Subject);
        Assert.Equal(source, lifecycleEvent.Source);
        var propertyEvent = result[1];
        Assert.Equal("Konnektr.Graph.Property.Event", propertyEvent.Type);
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
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "has")
        {
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
        Assert.Equal("Konnektr.Graph.Relationship.Lifecycle", cloudEvent.Type);
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
    public void CreateDataHistoryEvents_HandlesRelationshipCreateEventWithProperty()
    {
        // Arrange
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "has")
        {
            EventType = EventType.RelationshipCreate,
            NewValue = JsonNode
                .Parse(
                    "{\"$relationshipId\": \"rel1\", \"$sourceId\": \"twin1\", \"$targetId\": \"twin2\", \"$relationshipName\": \"has\", \"diameter\": 6.0}"
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

        var lifeCycleEvent = result.FirstOrDefault(ce =>
            ce.Type == "Konnektr.Graph.Relationship.Lifecycle"
        );
        Assert.NotNull(lifeCycleEvent);
        Assert.Equal("Konnektr.Graph.Relationship.Lifecycle", lifeCycleEvent.Type);
        Assert.Equal("application/json", lifeCycleEvent.DataContentType);
        Assert.Equal("twin1/relationships/rel1", lifeCycleEvent.Subject);
        Assert.Equal(source, lifeCycleEvent.Source);
        var lifeCycleData = lifeCycleEvent.Data as JsonObject;
        Assert.NotNull(lifeCycleData);
        Assert.Equal("twin1", lifeCycleData["source"]?.ToString());
        Assert.Equal("twin2", lifeCycleData["target"]?.ToString());
        Assert.Equal("has", lifeCycleData["name"]?.ToString());
        Assert.Equal("rel1", lifeCycleData["relationshipId"]?.ToString());
        Assert.Equal("Create", lifeCycleData["action"]?.ToString());

        var propertyEvent = result.FirstOrDefault(ce => ce.Type == "Konnektr.Graph.Property.Event");
        Assert.NotNull(propertyEvent);
        Assert.Equal("Konnektr.Graph.Property.Event", propertyEvent.Type);
        Assert.Equal("application/json", propertyEvent.DataContentType);
        Assert.Equal("twin1/relationships/rel1", propertyEvent.Subject);
        var propertyData = propertyEvent.Data as JsonObject;
        Assert.NotNull(propertyData);
        Assert.Equal("twin1", propertyData["id"]?.ToString());
        Assert.Equal("diameter", propertyData["key"]?.ToString());
        Assert.Equal("6.0", propertyData["value"]?.ToString());
        Assert.Equal("Create", propertyData["action"]?.ToString());
        Assert.Equal("rel1", propertyData["relationshipId"]?.ToString());
        Assert.Equal("twin2", propertyData["relationshipTarget"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_HandlesRelationshipUpdateEvent()
    {
        // Arrange
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "has")
        {
            EventType = EventType.RelationshipUpdate,
            OldValue = JsonNode
                .Parse(
                    "{\"$relationshipId\": \"rel1\", \"$sourceId\": \"twin1\", \"$targetId\": \"twin2\", \"$relationshipName\": \"has\", \"diameter\": 6.0}"
                )!
                .AsObject(),
            NewValue = JsonNode
                .Parse(
                    "{\"$relationshipId\": \"rel1\", \"$sourceId\": \"twin1\", \"$targetId\": \"twin2\", \"$relationshipName\": \"has\", \"diameter\": 5.0}"
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
        Assert.Equal("Konnektr.Graph.Property.Event", cloudEvent.Type);
        Assert.Equal("application/json", cloudEvent.DataContentType);
        Assert.Equal("twin1/relationships/rel1", cloudEvent.Subject);
        Assert.Equal(source, cloudEvent.Source);
        var data = cloudEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("twin1", data["id"]?.ToString());
        Assert.Equal("diameter", data["key"]?.ToString());
        Assert.Equal("5.0", data["value"]?.ToString());
        Assert.Equal("Update", data["action"]?.ToString());
        Assert.Equal("rel1", data["relationshipId"]?.ToString());
        Assert.Equal("twin2", data["relationshipTarget"]?.ToString());
    }

    [Fact]
    public void CreateDigitalTwinChangeNotificationEvents_SameValueUpdate_IncludesPropertyInPatch()
    {
        // Arrange - Property 'temperature' updated with same value, but lastUpdateTime changed
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
                return op is JsonObject opObj
                    && opObj["op"]?.ToString() == "replace"
                    && opObj["path"]?.ToString() == "/temperature"
                    && opObj["value"]?.ToString() == "25.5";
            }
        );
        Assert.Contains(
            patch,
            op =>
            {
                return op is JsonObject opObj
                    && opObj["op"]?.ToString() == "replace"
                    && opObj["path"]?.ToString() == "/$metadata/temperature/lastUpdateTime"
                    && opObj["value"]?.ToString() == "2024-01-15T10:30:00Z";
            }
        );
        Assert.DoesNotContain(
            patch,
            op =>
            {
                return op is JsonObject opObj && opObj["path"]?.ToString() == "/$dtId";
            }
        );
        Assert.DoesNotContain(
            patch,
            op =>
            {
                return op is JsonObject opObj && opObj["path"]?.ToString() == "/$etag";
            }
        );
    }

    [Fact]
    public void CreateDataHistoryEvents_ValueUpdate_CreatesPropertyEvent()
    {
        // Arrange - Property 'humidity' updated with new value
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
                    ""humidity"": 50.0
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
        Assert.Equal("Konnektr.Graph.Property.Event", propertyEvent.Type);
        Assert.Equal("twin1", propertyEvent.Subject);

        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("twin1", data["id"]?.ToString());
        Assert.Equal("humidity", data["key"]?.ToString());
        Assert.Equal("60.0", data["value"]?.ToString());
        Assert.Equal("Update", data["action"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_SameValueUpdate_CreatesPropertyEvent()
    {
        // Arrange - Property 'humidity' updated with same value, but lastUpdateTime changed
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
        Assert.Equal("Konnektr.Graph.Property.Event", propertyEvent.Type);
        Assert.Equal("twin1", propertyEvent.Subject);

        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("twin1", data["id"]?.ToString());
        Assert.Equal("humidity", data["key"]?.ToString());
        Assert.Equal("60.0", data["value"]?.ToString());
        Assert.Equal("Update", data["action"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedBy_IncludesUpdatedByInPropertyEvent()
    {
        // Arrange - Property 'temperature' updated, with lastUpdatedBy in metadata
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                            ""lastUpdatedBy"": ""user-123""
                        }
                    },
                    ""temperature"": 22.5
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
                    ""temperature"": 20.0
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act - with trackLastUpdatedBy=true
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        Assert.Single(result);
        var data = result[0].Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("temperature", data["key"]?.ToString());
        Assert.Equal("22.5", data["value"]?.ToString());
        Assert.Equal("user-123", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedByFalse_OmitsUpdatedByFromPropertyEvent()
    {
        // Arrange - Property 'temperature' updated, with lastUpdatedBy in metadata
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                            ""lastUpdatedBy"": ""user-123""
                        }
                    },
                    ""temperature"": 22.5
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
                    ""temperature"": 20.0
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act - with trackLastUpdatedBy=false (default)
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert
        Assert.Single(result);
        var data = result[0].Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("temperature", data["key"]?.ToString());
        Assert.False(data.ContainsKey("updatedBy"), "updatedBy should be absent when trackLastUpdatedBy is false");
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedByTrue_NoLastUpdatedByInMetadata_OmitsUpdatedBy()
    {
        // Arrange - Property updated, no lastUpdatedBy in metadata
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
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
                    ""temperature"": 22.5
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
                    ""temperature"": 20.0
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act - with trackLastUpdatedBy=true but no lastUpdatedBy in metadata
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        Assert.Single(result);
        var data = result[0].Data as JsonObject;
        Assert.NotNull(data);
        Assert.False(data.ContainsKey("updatedBy"), "updatedBy should be absent when not present in metadata");
    }

    [Fact]
    public void CreateDataHistoryEvents_TwinUpdate_TrackLastUpdatedBy_SameValueUpdate_IncludesUpdatedBy()
    {
        // Arrange - Property 'humidity' same value, but lastUpdatedBy added to metadata
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""humidity"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                            ""lastUpdatedBy"": ""user-456""
                        }
                    },
                    ""humidity"": 55.0
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
                    ""humidity"": 55.0
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert: one property event for the same-value update
        Assert.Single(result);
        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("humidity", data["key"]?.ToString());
        Assert.Equal("user-456", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedBy_IncludesUpdatedByInTwinLifecycleCreateEvent()
    {
        // Arrange - Twin created with $lastUpdatedBy in twin-level metadata
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""$lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                        ""$lastUpdatedBy"": ""twin-creator""
                    },
                    ""temperature"": 22.5
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert - includes lifecycle event + property events from patch
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Twin.Lifecycle");
        var data = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("twin1", data["twinId"]?.ToString());
        Assert.Equal("Create", data["action"]?.ToString());
        Assert.Equal("twin-creator", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedBy_IncludesUpdatedByInTwinLifecycleDeleteEvent()
    {
        // Arrange - Twin deleted, $lastUpdatedBy in OldValue's twin-level metadata
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinDelete,
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""$lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                        ""$lastUpdatedBy"": ""twin-deleter""
                    },
                    ""temperature"": 22.5
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert - includes lifecycle event + property events from patch
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Twin.Lifecycle");
        var data = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("twin1", data["twinId"]?.ToString());
        Assert.Equal("Delete", data["action"]?.ToString());
        Assert.Equal("twin-deleter", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedByTrue_NoLastUpdatedByInMetadata_OmitsUpdatedByInTwinLifecycle()
    {
        // Arrange - Twin created, no $lastUpdatedBy in metadata
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""$lastUpdateTime"": ""2024-01-15T10:30:00Z""
                    },
                    ""temperature"": 22.5
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert - includes lifecycle event + property events from patch
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Twin.Lifecycle");
        var data = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.False(data.ContainsKey("updatedBy"), "updatedBy should be absent when not present in twin metadata");
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedByFalse_OmitsUpdatedByInTwinLifecycle()
    {
        // Arrange - Twin created with $lastUpdatedBy but trackLastUpdatedBy=false
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""$lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                        ""$lastUpdatedBy"": ""twin-creator""
                    },
                    ""temperature"": 22.5
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act - default trackLastUpdatedBy=false
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert - includes lifecycle event + property events from patch
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Twin.Lifecycle");
        var data = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.False(data.ContainsKey("updatedBy"), "updatedBy should be absent when trackLastUpdatedBy is false");
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedBy_BundledMetadata_IncludesUpdatedBy()
    {
        // Arrange - Property 'label' first write, CreatePatch produces bundled $metadata/label operation
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": { ""$model"": ""model1"" }
                }"
                )!
                .AsObject(),
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""label"": {
                            ""lastUpdateTime"": ""2024-06-17T11:32:39.4410855Z"",
                            ""lastUpdatedBy"": ""9c00982e-6218-42c1-bf77-fb5e5d339d8e""
                        }
                    },
                    ""label"": ""qsdqsdqsdqsdd""
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("label", data["key"]?.ToString());
        Assert.Equal("qsdqsdqsdqsdd", data["value"]?.ToString());
        Assert.Equal("9c00982e-6218-42c1-bf77-fb5e5d339d8e", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedByFalse_BundledMetadata_OmitsUpdatedBy()
    {
        // Arrange - Property 'label' first write, bundled metadata, but trackLastUpdatedBy=false
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": { ""$model"": ""model1"" }
                }"
                )!
                .AsObject(),
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""label"": {
                            ""lastUpdateTime"": ""2024-06-17T11:32:39.4410855Z"",
                            ""lastUpdatedBy"": ""9c00982e-6218-42c1-bf77-fb5e5d339d8e""
                        }
                    },
                    ""label"": ""qsdqsdqsdqsdd""
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act - default trackLastUpdatedBy=false
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert
        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("label", data["key"]?.ToString());
        Assert.False(data.ContainsKey("updatedBy"), "updatedBy should be absent when trackLastUpdatedBy is false");
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedBy_BundledMetadataWithSourceTime_IncludesSourceTime()
    {
        // Arrange - Property 'label' first write, bundled metadata includes sourceTime
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": { ""$model"": ""model1"" }
                }"
                )!
                .AsObject(),
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""label"": {
                            ""lastUpdateTime"": ""2024-06-17T11:32:39.4410855Z"",
                            ""sourceTime"": ""2024-06-17T11:30:00Z"",
                            ""lastUpdatedBy"": ""9c00982e-6218-42c1-bf77-fb5e5d339d8e""
                        }
                    },
                    ""label"": ""qsdqsdqsdqsdd""
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("label", data["key"]?.ToString());
        Assert.Equal("2024-06-17T11:30:00Z", data["sourceTimeStamp"]?.ToString());
        Assert.Equal("9c00982e-6218-42c1-bf77-fb5e5d339d8e", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedBy_BundledMetadata_SubsequentWriteStillWorks()
    {
        // Arrange - Property 'temperature' updated (subsequent write), separate metadata operations
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                            ""lastUpdatedBy"": ""user-123""
                        }
                    },
                    ""temperature"": 22.5
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
                    ""temperature"": 20.0
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert - the existing behavior still works
        Assert.Single(result);
        var data = result[0].Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("temperature", data["key"]?.ToString());
        Assert.Equal("22.5", data["value"]?.ToString());
        Assert.Equal("user-123", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TwinCreate_TrackLastUpdatedBy_IncludesUpdatedByInPropertyEvents()
    {
        // Arrange - Twin created with per-property lastUpdatedBy (not twin-level)
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""label"": {
                            ""lastUpdateTime"": ""2024-06-17T11:32:39.4410855Z"",
                            ""lastUpdatedBy"": ""9c00982e-6218-42c1-bf77-fb5e5d339d8e""
                        }
                    },
                    ""label"": ""qsdqsdqsdqsdd""
                }"
                )!
                .AsObject(),
            OldValue = new JsonObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert - includes lifecycle event (no twin-level $lastUpdatedBy) + property events
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Twin.Lifecycle");
        var lifecycleData = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(lifecycleData);
        Assert.Equal("twin1", lifecycleData["twinId"]?.ToString());
        Assert.Equal("Create", lifecycleData["action"]?.ToString());
        Assert.Null(lifecycleData["updatedBy"]?.ToString());

        // Property event should have per-property updatedBy
        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var propertyData = propertyEvent.Data as JsonObject;
        Assert.NotNull(propertyData);
        Assert.Equal("label", propertyData["key"]?.ToString());
        Assert.Equal("qsdqsdqsdqsdd", propertyData["value"]?.ToString());
        Assert.Equal("9c00982e-6218-42c1-bf77-fb5e5d339d8e", propertyData["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TwinCreate_TrackLastUpdatedBy_BothLevels_IncludesUpdatedByInBoth()
    {
        // Arrange - Twin created with both twin-level $lastUpdatedBy and per-property lastUpdatedBy
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""$lastUpdatedBy"": ""twin-creator"",
                        ""label"": {
                            ""lastUpdateTime"": ""2024-06-17T11:32:39.4410855Z"",
                            ""lastUpdatedBy"": ""9c00982e-6218-42c1-bf77-fb5e5d339d8e""
                        }
                    },
                    ""label"": ""qsdqsdqsdqsdd""
                }"
                )!
                .AsObject(),
            OldValue = new JsonObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Twin.Lifecycle");
        var lifecycleData = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(lifecycleData);
        Assert.Equal("twin-creator", lifecycleData["updatedBy"]?.ToString());

        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var propertyData = propertyEvent.Data as JsonObject;
        Assert.NotNull(propertyData);
        Assert.Equal("9c00982e-6218-42c1-bf77-fb5e5d339d8e", propertyData["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TwinCreate_TrackLastUpdatedByFalse_OmitsUpdatedByInPropertyEvents()
    {
        // Arrange - Twin created with per-property lastUpdatedBy but trackLastUpdatedBy=false
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""label"": {
                            ""lastUpdateTime"": ""2024-06-17T11:32:39.4410855Z"",
                            ""lastUpdatedBy"": ""9c00982e-6218-42c1-bf77-fb5e5d339d8e""
                        }
                    },
                    ""label"": ""qsdqsdqsdqsdd""
                }"
                )!
                .AsObject(),
            OldValue = new JsonObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act - default trackLastUpdatedBy=false
        var result = CloudEventFactory.CreateDataHistoryEvents(eventData, source, []);

        // Assert
        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var propertyData = propertyEvent.Data as JsonObject;
        Assert.NotNull(propertyData);
        Assert.False(propertyData.ContainsKey("updatedBy"), "updatedBy should be absent when trackLastUpdatedBy is false");
    }

    [Fact]
    public void CreateDataHistoryEvents_Update_BundledMetadataWithSourceTime_IncludesSourceTime()
    {
        // Arrange - Property first added on existing twin, sourceTime in bundled $metadata/<prop>
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1""
                    }
                }"
                )!
                .AsObject(),
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""label"": {
                            ""lastUpdateTime"": ""2024-06-17T11:32:39.4410855Z"",
                            ""sourceTime"": ""2024-06-17T11:30:00Z"",
                            ""lastUpdatedBy"": ""9c00982e-6218-42c1-bf77-fb5e5d339d8e""
                        }
                    },
                    ""label"": ""qsdqsdqsdqsdd""
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("label", data["key"]?.ToString());
        Assert.Equal("2024-06-17T11:30:00Z", data["sourceTimeStamp"]?.ToString());
        Assert.Equal("9c00982e-6218-42c1-bf77-fb5e5d339d8e", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TwinCreate_BundledMetadataWithSourceTime_IncludesSourceTime()
    {
        // Arrange - Twin created with sourceTime in per-property metadata (top-level bundle)
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""label"": {
                            ""lastUpdateTime"": ""2024-06-17T11:32:39.4410855Z"",
                            ""sourceTime"": ""2024-06-17T11:30:00Z"",
                            ""lastUpdatedBy"": ""9c00982e-6218-42c1-bf77-fb5e5d339d8e""
                        }
                    },
                    ""label"": ""qsdqsdqsdqsdd""
                }"
                )!
                .AsObject(),
            OldValue = new JsonObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        var propertyEvent = result.First(e => e.Type == "Konnektr.Graph.Property.Event");
        var data = propertyEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("label", data["key"]?.ToString());
        Assert.Equal("2024-06-17T11:30:00Z", data["sourceTimeStamp"]?.ToString());
        Assert.Equal("9c00982e-6218-42c1-bf77-fb5e5d339d8e", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_SubsequentWrite_SeparateSourceTimeOperation_IncludesSourceTime()
    {
        // Arrange - Property value change on existing twin, sourceTime as separate metadata op
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                            ""sourceTime"": ""2024-01-15T10:25:00Z"",
                            ""lastUpdatedBy"": ""user-123""
                        }
                    },
                    ""temperature"": 22.5
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
                    ""temperature"": 20.0
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert - separate operations: /$metadata/temperature/sourceTime
        Assert.Single(result);
        var data = result[0].Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("temperature", data["key"]?.ToString());
        Assert.Equal("2024-01-15T10:25:00Z", data["sourceTimeStamp"]?.ToString());
        Assert.Equal("user-123", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedBy_SameUserRepeatUpdate_FallsBackToNewValueMetadata()
    {
        // Arrange - Property updated by same user twice, lastUpdatedBy unchanged in patch,
        // so the fallback reads it directly from NewValue.$metadata
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:00:00Z"",
                            ""lastUpdatedBy"": ""same-user-id""
                        }
                    },
                    ""temperature"": 20.0
                }"
                )!
                .AsObject(),
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                            ""lastUpdatedBy"": ""same-user-id""
                        }
                    },
                    ""temperature"": 22.5
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert - the patch does NOT contain /$metadata/temperature/lastUpdatedBy
        // because the value didn't change, but the fallback still picks it up from NewValue
        Assert.Single(result);
        var data = result[0].Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("temperature", data["key"]?.ToString());
        Assert.Equal("22.5", data["value"]?.ToString());
        Assert.Equal("same-user-id", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateDataHistoryEvents_TrackLastUpdatedBy_PropertyUpdatedByDiffUser_WorksViaPatch()
    {
        // Arrange - Property updated by a DIFFERENT user, so lastUpdatedBy IS in the patch
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Twin")
        {
            EventType = EventType.TwinUpdate,
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:00:00Z"",
                            ""lastUpdatedBy"": ""old-user""
                        }
                    },
                    ""temperature"": 20.0
                }"
                )!
                .AsObject(),
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$dtId"": ""twin1"",
                    ""$metadata"": {
                        ""$model"": ""model1"",
                        ""temperature"": {
                            ""lastUpdateTime"": ""2024-01-15T10:30:00Z"",
                            ""lastUpdatedBy"": ""new-user""
                        }
                    },
                    ""temperature"": 22.5
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateDataHistoryEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert - the patch contains /$metadata/temperature/lastUpdatedBy because the user changed
        Assert.Single(result);
        var data = result[0].Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("temperature", data["key"]?.ToString());
        Assert.Equal("new-user", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateRelationshipLifeCycleEvents_TrackLastUpdatedBy_IncludesUpdatedBy()
    {
        // Arrange
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Relationship")
        {
            EventType = EventType.RelationshipCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$relationshipId"": ""rel1"",
                    ""$relationshipName"": ""contains"",
                    ""$sourceId"": ""room1"",
                    ""$targetId"": ""sensor1"",
                    ""$metadata"": {
                        ""$model"": ""dtmi:com:adt:dtsample:room-contains-sensor;1"",
                        ""$lastUpdatedBy"": ""user-42""
                    }
                }"
                )!
                .AsObject(),
            OldValue = null,
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateRelationshipLifeCycleEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Relationship.Lifecycle");
        var data = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("rel1", data["relationshipId"]?.ToString());
        Assert.Equal("room1", data["source"]?.ToString());
        Assert.Equal("sensor1", data["target"]?.ToString());
        Assert.Equal("user-42", data["updatedBy"]?.ToString());
    }

    [Fact]
    public void CreateRelationshipLifeCycleEvents_TrackLastUpdatedByFalse_OmitsUpdatedBy()
    {
        // Arrange
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Relationship")
        {
            EventType = EventType.RelationshipCreate,
            NewValue = JsonNode
                .Parse(
                    @"{
                    ""$relationshipId"": ""rel1"",
                    ""$relationshipName"": ""contains"",
                    ""$sourceId"": ""room1"",
                    ""$targetId"": ""sensor1"",
                    ""$metadata"": {
                        ""$model"": ""dtmi:com:adt:dtsample:room-contains-sensor;1"",
                        ""$lastUpdatedBy"": ""user-42""
                    }
                }"
                )!
                .AsObject(),
            OldValue = null,
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act - default trackLastUpdatedBy=false
        var result = CloudEventFactory.CreateRelationshipLifeCycleEvents(eventData, source, []);

        // Assert
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Relationship.Lifecycle");
        var data = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.False(data.ContainsKey("updatedBy"), "updatedBy should be absent when trackLastUpdatedBy is false");
    }

    [Fact]
    public void CreateRelationshipLifeCycleEvents_Delete_TrackLastUpdatedBy_ReadsFromOldValue()
    {
        // Arrange - On delete, the OldValue should be used for $lastUpdatedBy
        var eventData = new EventData(Guid.NewGuid().ToString(), "digitaltwins", "Relationship")
        {
            EventType = EventType.RelationshipDelete,
            NewValue = null,
            OldValue = JsonNode
                .Parse(
                    @"{
                    ""$relationshipId"": ""rel1"",
                    ""$relationshipName"": ""contains"",
                    ""$sourceId"": ""room1"",
                    ""$targetId"": ""sensor1"",
                    ""$metadata"": {
                        ""$model"": ""dtmi:com:adt:dtsample:room-contains-sensor;1"",
                        ""$lastUpdatedBy"": ""deleting-user""
                    }
                }"
                )!
                .AsObject(),
            Timestamp = DateTime.UtcNow,
        };
        var source = new Uri("http://example.com");

        // Act
        var result = CloudEventFactory.CreateRelationshipLifeCycleEvents(
            eventData,
            source,
            [],
            trackLastUpdatedBy: true
        );

        // Assert
        var lifecycleEvent = result.First(e => e.Type == "Konnektr.Graph.Relationship.Lifecycle");
        var data = lifecycleEvent.Data as JsonObject;
        Assert.NotNull(data);
        Assert.Equal("Delete", data["action"]?.ToString());
        Assert.Equal("deleting-user", data["updatedBy"]?.ToString());
    }
}
