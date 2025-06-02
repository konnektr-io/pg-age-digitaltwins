using System.Text.Json.Nodes;
using CloudNative.CloudEvents;
using Json.More;
using Json.Patch;

namespace AgeDigitalTwins.Events;

public static class CloudEventFactory
{
    // Default CloudEvent type mappings for EventNotification
    public static readonly Dictionary<SinkEventType, string> DefaultEventNotificationTypeMapping =
        new()
        {
            { SinkEventType.TwinCreate, "Konnektr.DigitalTwins.Twin.Create" },
            { SinkEventType.TwinUpdate, "Konnektr.DigitalTwins.Twin.Update" },
            { SinkEventType.TwinDelete, "Konnektr.DigitalTwins.Twin.Delete" },
            { SinkEventType.RelationshipCreate, "Konnektr.DigitalTwins.Relationship.Create" },
            { SinkEventType.RelationshipUpdate, "Konnektr.DigitalTwins.Relationship.Update" },
            { SinkEventType.RelationshipDelete, "Konnektr.DigitalTwins.Relationship.Delete" },
        };

    // Default CloudEvent type mappings for DataHistory
    public static readonly Dictionary<SinkEventType, string> DefaultDataHistoryTypeMapping =
        new()
        {
            { SinkEventType.PropertyEvent, "Konnektr.DigitalTwins.Property.Event" },
            { SinkEventType.TwinLifecycle, "Konnektr.DigitalTwins.Twin.Lifecycle" },
            { SinkEventType.RelationshipLifecycle, "Konnektr.DigitalTwins.Relationship.Lifecycle" },
        };

    #region EventNotification

    public static List<CloudEvent> CreateEventNotificationEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string>? typeMapping = null
    )
    {
        var mapping = typeMapping ?? DefaultEventNotificationTypeMapping;
        return eventData.EventType switch
        {
            EventType.TwinCreate => CreateDigitalTwinLifecycleNotificationEvents(
                eventData,
                source,
                mapping
            ),
            EventType.TwinUpdate => CreateDigitalTwinChangeNotificationEvents(
                eventData,
                source,
                mapping
            ),
            EventType.TwinDelete => CreateDigitalTwinLifecycleNotificationEvents(
                eventData,
                source,
                mapping
            ),
            EventType.RelationshipCreate => CreateRelationshipLifecycleNotificationEvents(
                eventData,
                source,
                mapping
            ),
            EventType.RelationshipUpdate => CreateRelationshipChangeNotificationEvents(
                eventData,
                source,
                mapping
            ),
            EventType.RelationshipDelete => CreateRelationshipLifecycleNotificationEvents(
                eventData,
                source,
                mapping
            ),
            _ => throw new ArgumentException(
                "EventType must be TwinCreate, TwinUpdate, TwinDelete, RelationshipCreate, RelationshipUpdate, or RelationshipDelete",
                nameof(eventData)
            ),
        };
    }

    public static List<CloudEvent> CreateDigitalTwinChangeNotificationEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string> typeMapping
    )
    {
        if (
            eventData == null
            || eventData.EventType != EventType.TwinUpdate
            || eventData.NewValue == null
            || eventData.OldValue == null
        )
        {
            throw new ArgumentNullException(nameof(eventData));
        }
        if (
            !eventData.NewValue.TryGetPropertyValue("$dtId", out JsonNode? twinIdNode)
            || twinIdNode == null
        )
        {
            throw new ArgumentException(
                "NewValue must contain a $dtId property",
                nameof(eventData)
            );
        }
        JsonPatch jsonPatch = eventData.OldValue.CreatePatch(eventData.NewValue);
        JsonObject body =
            new()
            {
                ["modelId"] = eventData.NewValue["$metadata"]?["$model"]?.DeepClone(),
                ["patch"] = JsonNode.Parse(jsonPatch.ToJsonDocument().RootElement.GetRawText()),
            };
        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = typeMapping.TryGetValue(SinkEventType.TwinUpdate, out var t)
                    ? t
                    : DefaultEventNotificationTypeMapping[SinkEventType.TwinUpdate],
                DataContentType = "application/json",
                Subject = twinIdNode.ToString(),
                Time = eventData.Timestamp,
            };
        return [cloudEvent];
    }

    public static List<CloudEvent> CreateDigitalTwinLifecycleNotificationEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string> typeMapping
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);

        JsonObject body;
        string type;
        if (eventData.EventType == EventType.TwinCreate)
        {
            if (eventData.NewValue == null)
            {
                throw new ArgumentException(
                    "NewValue cannot be null for TwinCreate event",
                    nameof(eventData)
                );
            }
            body = eventData.NewValue;
            type = typeMapping.TryGetValue(SinkEventType.TwinCreate, out var t)
                ? t
                : DefaultEventNotificationTypeMapping[SinkEventType.TwinCreate];
        }
        else if (eventData.EventType == EventType.TwinDelete)
        {
            if (eventData.OldValue == null)
            {
                throw new ArgumentException(
                    "OldValue cannot be null for TwinDelete event",
                    nameof(eventData)
                );
            }
            body = eventData.OldValue;
            type = typeMapping.TryGetValue(SinkEventType.TwinDelete, out var t)
                ? t
                : DefaultEventNotificationTypeMapping[SinkEventType.TwinDelete];
        }
        else
        {
            throw new ArgumentException(
                "EventType must be TwinCreate or TwinDelete",
                nameof(eventData)
            );
        }

        if (!body.TryGetPropertyValue("$dtId", out JsonNode? twinIdNode) || twinIdNode == null)
        {
            throw new ArgumentException(
                "NewValue must contain a $dtId property",
                nameof(eventData)
            );
        }

        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = type,
                DataContentType = "application/json",
                Subject = twinIdNode.ToString(),
                Time = eventData.Timestamp,
            };

        return [cloudEvent];
    }

    public static List<CloudEvent> CreateRelationshipChangeNotificationEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string> typeMapping
    )
    {
        if (
            eventData == null
            || eventData.EventType != EventType.RelationshipUpdate
            || eventData.NewValue == null
            || eventData.OldValue == null
        )
        {
            throw new ArgumentNullException(nameof(eventData));
        }
        if (
            !eventData.NewValue.TryGetPropertyValue(
                "$relationshipId",
                out JsonNode? relationshipIdNode
            )
            || relationshipIdNode == null
        )
        {
            throw new ArgumentException(
                "NewValue must contain a $relationshipId property",
                nameof(eventData)
            );
        }

        if (
            !eventData.NewValue.TryGetPropertyValue("$sourceId", out JsonNode? twinIdNode)
            || twinIdNode == null
        )
        {
            throw new ArgumentException(
                "NewValue must contain a $sourceId property",
                nameof(eventData)
            );
        }
        JsonPatch jsonPatch = eventData.OldValue.CreatePatch(eventData.NewValue);
        JsonObject body =
            new()
            {
                ["modelId"] = eventData.NewValue["$metadata"]?["$model"]?.DeepClone(),
                ["patch"] = JsonNode.Parse(jsonPatch.ToJsonDocument().RootElement.GetRawText()),
            };
        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = typeMapping.TryGetValue(SinkEventType.RelationshipUpdate, out var t)
                    ? t
                    : DefaultEventNotificationTypeMapping[SinkEventType.RelationshipUpdate],
                DataContentType = "application/json",
                Subject = $"{twinIdNode}/relationships/{relationshipIdNode}",
                Time = eventData.Timestamp,
            };

        return [cloudEvent];
    }

    public static List<CloudEvent> CreateRelationshipLifecycleNotificationEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string> typeMapping
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);

        JsonObject body;
        string type;
        if (eventData.EventType == EventType.RelationshipCreate)
        {
            if (eventData.NewValue == null)
            {
                throw new ArgumentException(
                    "NewValue cannot be null for RelationshipCreate event",
                    nameof(eventData)
                );
            }
            body = eventData.NewValue;
            type = typeMapping.TryGetValue(SinkEventType.RelationshipCreate, out var t)
                ? t
                : DefaultEventNotificationTypeMapping[SinkEventType.RelationshipCreate];
        }
        else if (eventData.EventType == EventType.RelationshipDelete)
        {
            if (eventData.OldValue == null)
            {
                throw new ArgumentException(
                    "OldValue cannot be null for RelationshipDelete event",
                    nameof(eventData)
                );
            }
            body = eventData.OldValue;
            type = typeMapping.TryGetValue(SinkEventType.RelationshipDelete, out var t)
                ? t
                : DefaultEventNotificationTypeMapping[SinkEventType.RelationshipDelete];
        }
        else
        {
            throw new ArgumentException(
                "EventType must be RelationshipCreate or RelationshipDelete",
                nameof(eventData)
            );
        }

        if (
            !body.TryGetPropertyValue("$relationshipId", out JsonNode? relationshipIdNode)
            || relationshipIdNode == null
        )
        {
            throw new ArgumentException(
                "NewValue must contain a $relationshipId property",
                nameof(eventData)
            );
        }

        if (!body.TryGetPropertyValue("$sourceId", out JsonNode? twinIdNode) || twinIdNode == null)
        {
            throw new ArgumentException(
                "NewValue must contain a $sourceId property",
                nameof(eventData)
            );
        }

        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = type,
                DataContentType = "application/json",
                Subject = $"{twinIdNode}/relationships/{relationshipIdNode}",
                Time = eventData.Timestamp,
            };

        return [cloudEvent];
    }

    #endregion

    #region DataHistory

    public static List<CloudEvent> CreateDataHistoryEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string>? typeMapping = null
    )
    {
        var mapping = typeMapping ?? DefaultDataHistoryTypeMapping;
        return eventData.EventType switch
        {
            EventType.TwinCreate or EventType.TwinDelete => CreateTwinLifeCycleEvents(
                eventData,
                source,
                mapping
            ),
            EventType.RelationshipCreate or EventType.RelationshipDelete =>
                CreateRelationshipLifeCycleEvents(eventData, source, mapping),
            EventType.TwinUpdate or EventType.RelationshipUpdate => CreatePropertyEvents(
                eventData,
                source,
                mapping
            ),
            _ => throw new ArgumentException(
                "EventType must be TwinCreate, TwinUpdate, TwinDelete, RelationshipCreate, RelationshipUpdate, or RelationshipDelete",
                nameof(eventData)
            ),
        };
    }

    public static List<CloudEvent> CreateTwinLifeCycleEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string> typeMapping
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);
        List<CloudEvent> cloudEvents = [];
        JsonObject body =
            new()
            {
                ["twinId"] =
                    eventData.NewValue?["$dtId"]?.ToString()
                    ?? eventData.OldValue?["$dtId"]?.ToString(),
                ["action"] = eventData.EventType switch
                {
                    EventType.TwinCreate => "Create",
                    EventType.TwinDelete => "Delete",
                    _ => "unknown",
                },
                ["timeStamp"] = eventData.Timestamp,
                ["serviceId"] = source.ToString(),
                ["modelId"] =
                    eventData.NewValue?["$metadata"]?["$model"]?.ToString()
                    ?? eventData.OldValue?["$metadata"]?["$model"]?.ToString(),
            };
        var type = typeMapping.TryGetValue(SinkEventType.TwinLifecycle, out var t)
            ? t
            : DefaultDataHistoryTypeMapping[SinkEventType.TwinLifecycle];
        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = type,
                DataContentType = "application/json",
                Subject = body["twinId"]?.ToString(),
                Time = eventData.Timestamp,
            };
        cloudEvents.Add(cloudEvent);
        cloudEvents.AddRange(CreateCloudEventsFromPatch(eventData, source, typeMapping));
        return cloudEvents;
    }

    public static List<CloudEvent> CreateRelationshipLifeCycleEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string> typeMapping
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);
        List<CloudEvent> cloudEvents = [];
        JsonObject body =
            new()
            {
                ["relationshipId"] =
                    eventData.NewValue?["$relationshipId"]?.ToString()
                    ?? eventData.OldValue?["$relationshipId"]?.ToString(),
                ["action"] = eventData.EventType switch
                {
                    EventType.RelationshipCreate => "Create",
                    EventType.RelationshipDelete => "Delete",
                    _ => "unknown",
                },
                ["timeStamp"] = eventData.Timestamp,
                ["serviceId"] = source.ToString(),
                ["name"] =
                    eventData.NewValue?["name"]?.ToString()
                    ?? eventData.OldValue?["name"]?.ToString(),
                ["source"] =
                    eventData.NewValue?["$sourceId"]?.ToString()
                    ?? eventData.OldValue?["$sourceId"]?.ToString(),
                ["target"] =
                    eventData.NewValue?["$targetId"]?.ToString()
                    ?? eventData.OldValue?["$targetId"]?.ToString(),
            };
        var type = typeMapping.TryGetValue(SinkEventType.RelationshipLifecycle, out var t)
            ? t
            : DefaultDataHistoryTypeMapping[SinkEventType.RelationshipLifecycle];
        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = type,
                DataContentType = "application/json",
                Subject = $"{body["source"]}/relationships/{body["relationshipId"]}",
                Time = eventData.Timestamp,
            };
        cloudEvents.Add(cloudEvent);
        cloudEvents.AddRange(CreateCloudEventsFromPatch(eventData, source, typeMapping));
        return cloudEvents;
    }

    public static List<CloudEvent> CreatePropertyEvents(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string> typeMapping
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);
        List<CloudEvent> cloudEvents = [];
        // Data model changes generate lifecycle events, not property events.
        if (
            eventData.NewValue?["$metadata"]?["$model"]?.ToString()
            != eventData.OldValue?["$metadata"]?["$model"]?.ToString()
        )
        {
            JsonObject body =
                new()
                {
                    ["twinId"] = eventData.NewValue?["$dtId"]?.ToString(),
                    ["action"] = "Update",
                    ["timeStamp"] = eventData.Timestamp,
                    ["serviceId"] = source.ToString(),
                    ["modelId"] = eventData.NewValue?["modelId"]?.ToString(),
                };
            var type = typeMapping.TryGetValue(SinkEventType.TwinLifecycle, out var t)
                ? t
                : DefaultDataHistoryTypeMapping[SinkEventType.TwinLifecycle];
            CloudEvent cloudEvent =
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = source,
                    Data = body,
                    Type = type,
                    DataContentType = "application/json",
                    Subject = body["twinId"]?.ToString(),
                    Time = eventData.Timestamp,
                };
            cloudEvents.Add(cloudEvent);
        }
        cloudEvents.AddRange(CreateCloudEventsFromPatch(eventData, source, typeMapping));
        return cloudEvents;
    }

    private static List<CloudEvent> CreateCloudEventsFromPatch(
        EventData eventData,
        Uri source,
        Dictionary<SinkEventType, string> typeMapping
    )
    {
        JsonPatch jsonPatch = eventData.OldValue.CreatePatch(eventData.NewValue);
        List<CloudEvent> cloudEvents = [];
        foreach (PatchOperation op in jsonPatch.Operations)
        {
            if (op.Path.ToString().StartsWith("/$"))
            {
                continue;
            }
            if (op.Path.Count == 0 && op.Value == null)
            {
                // Skip empty operations
                continue;
            }
            string key = op.Path.ToString().Trim('/').Replace("/", "_");
            JsonObject body =
                new()
                {
                    ["timeStamp"] = eventData.Timestamp,
                    ["serviceId"] = source.ToString(),
                    ["id"] =
                        eventData.NewValue?["$dtId"]?.ToString()
                        ?? eventData.NewValue?["$sourceId"]?.ToString(),
                    ["modelId"] = eventData.NewValue?["$metadata"]?["$model"]?.ToString(),
                    ["key"] = key,
                    ["value"] = op.Value,
                    ["relationshipTarget"] = eventData.NewValue?["$targetId"]?.ToString(),
                    ["relationshipId"] = eventData.NewValue?["$relationshipId"]?.ToString(),
                    ["action"] = op.Op switch
                    {
                        OperationType.Add => "Create",
                        OperationType.Remove => "Delete",
                        OperationType.Replace => "Update",
                        _ => "unknown",
                    },
                };
            PatchOperation? sourceTimeOperation = jsonPatch.Operations.FirstOrDefault(o =>
                o.Path.ToString() == $"/$metadata/{key}/sourceTime"
            );
            if (sourceTimeOperation != null)
            {
                body["sourceTimeStamp"] = sourceTimeOperation.Value;
            }
            var type = typeMapping.TryGetValue(SinkEventType.PropertyEvent, out var t)
                ? t
                : DefaultDataHistoryTypeMapping[SinkEventType.PropertyEvent];
            CloudEvent cloudEvent =
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = source,
                    Data = body,
                    Type = type,
                    DataContentType = "application/json",
                    Subject = string.IsNullOrEmpty(body["relationshipId"]?.ToString())
                        ? $"{body["id"]}"
                        : $"{body["id"]}/relationships/{body["relationshipId"]}",
                    Time = eventData.Timestamp,
                };
            cloudEvents.Add(cloudEvent);
        }
        return cloudEvents;
    }

    #endregion
}
