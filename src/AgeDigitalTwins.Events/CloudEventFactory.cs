using System.Text.Json;
using System.Text.Json.Nodes;
using CloudNative.CloudEvents;
using Json.Patch;

namespace AgeDigitalTwins.Events;

public static class CloudEventFactory
{
    #region EventNotification

    public static List<CloudEvent> CreateEventNotificationEvents(EventData eventData, Uri source)
    {
        return eventData.EventType switch
        {
            EventType.TwinCreate => CreateDigitalTwinLifecycleNotificationEvents(eventData, source),
            EventType.TwinUpdate => CreateDigitalTwinChangeNotificationEvents(eventData, source),
            EventType.TwinDelete => CreateDigitalTwinLifecycleNotificationEvents(eventData, source),
            EventType.RelationshipCreate => CreateRelationshipLifecycleNotificationEvents(
                eventData,
                source
            ),
            EventType.RelationshipUpdate => CreateRelationshipChangeNotificationEvents(
                eventData,
                source
            ),
            EventType.RelationshipDelete => CreateRelationshipLifecycleNotificationEvents(
                eventData,
                source
            ),
            _ => throw new ArgumentException(
                "EventType must be TwinCreate, TwinUpdate, TwinDelete, RelationshipCreate, RelationshipUpdate, or RelationshipDelete",
                nameof(eventData)
            ),
        };
    }

    public static List<CloudEvent> CreateDigitalTwinChangeNotificationEvents(
        EventData eventData,
        Uri source
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
                ["modelId"] = eventData.NewValue["modelId"]?.DeepClone(),
                ["patch"] = JsonSerializer.Deserialize<JsonArray>(jsonPatch.ToString() ?? "[]"),
            };
        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = "Konnektr.DigitalTwins.Twin.Update", // "Microsoft.DigitalTwins.Twin.Update",
                DataContentType = "application/json",
                Subject = twinIdNode.ToString(),
                Time = eventData.Timestamp,
                // TraceParent = null,
            };

        return [cloudEvent];
    }

    public static List<CloudEvent> CreateDigitalTwinLifecycleNotificationEvents(
        EventData eventData,
        Uri source
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
            type = "Konnektr.DigitalTwins.Twin.Create"; // "Microsoft.DigitalTwins.Twin.Create";
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
            type = "Konnektr.DigitalTwins.Twin.Delete"; // "Microsoft.DigitalTwins.Twin.Delete";
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
                // TraceParent = null,
            };

        return [cloudEvent];
    }

    public static List<CloudEvent> CreateRelationshipChangeNotificationEvents(
        EventData eventData,
        Uri source
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
                ["modelId"] = eventData.NewValue["modelId"]?.DeepClone(),
                ["patch"] = JsonSerializer.Deserialize<JsonArray>(jsonPatch.ToString() ?? "[]"),
            };
        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = "Konnektr.DigitalTwins.Relationship.Update", // "Microsoft.DigitalTwins.Relationship.Update",
                DataContentType = "application/json",
                Subject = $"{twinIdNode}/relationships/{relationshipIdNode}",
                Time = eventData.Timestamp,
                // TraceParent = null,
            };

        return [cloudEvent];
    }

    public static List<CloudEvent> CreateRelationshipLifecycleNotificationEvents(
        EventData eventData,
        Uri source
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
            type = "Konnektr.DigitalTwins.Relationship.Create"; // "Microsoft.DigitalTwins.Relationship.Create";
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
            type = "Konnektr.DigitalTwins.Relationship.Delete"; // "Microsoft.DigitalTwins.Relationship.Delete";
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

        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = type,
                DataContentType = "application/json",
                Subject = relationshipIdNode.ToString(),
                Time = eventData.Timestamp,
                // TraceParent = null,
            };

        return [cloudEvent];
    }

    #endregion

    #region DataHistory

    public static List<CloudEvent> CreateDataHistoryEvents(EventData eventData, Uri source)
    {
        return eventData.EventType switch
        {
            EventType.TwinCreate or EventType.TwinDelete => CreateTwinLifeCycleEvents(
                eventData,
                source
            ),
            EventType.RelationshipCreate or EventType.RelationshipDelete =>
                CreateRelationshipLifeCycleEvents(eventData, source),
            EventType.TwinUpdate or EventType.RelationshipUpdate => CreatePropertyEvents(
                eventData,
                source
            ),
            _ => throw new ArgumentException(
                "EventType must be TwinCreate, TwinUpdate, TwinDelete, RelationshipCreate, RelationshipUpdate, or RelationshipDelete",
                nameof(eventData)
            ),
        };
    }

    public static List<CloudEvent> CreateTwinLifeCycleEvents(EventData eventData, Uri source)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        JsonObject body =
            new()
            {
                ["twinId"] =
                    eventData.NewValue?["$dtId"]?.ToString()
                    ?? eventData.OldValue?["$dtId"]?.ToString(),
                ["action"] = eventData.EventType.ToString(),
                ["timestamp"] = eventData.Timestamp,
                ["serviceId"] = source.ToString(),
                ["modelId"] =
                    eventData.NewValue?["modelId"]?.ToString()
                    ?? eventData.OldValue?["modelId"]?.ToString(),
            };

        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = "Konnektr.DigitalTwins.Twin.Lifecycle",
                DataContentType = "application/json",
                Subject = body["twinId"]?.ToString(),
                Time = eventData.Timestamp,
                // TraceParent = null,
            };

        return [cloudEvent];
    }

    public static List<CloudEvent> CreateRelationshipLifeCycleEvents(
        EventData eventData,
        Uri source
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);

        JsonObject body =
            new()
            {
                ["relationshipId"] =
                    eventData.NewValue?["$relationshipId"]?.ToString()
                    ?? eventData.OldValue?["$relationshipId"]?.ToString(),
                ["action"] = eventData.EventType.ToString(),
                ["timestamp"] = eventData.Timestamp,
                ["serviceId"] = source.Host.ToString(),
                ["name"] =
                    eventData.NewValue?["name"]?.ToString()
                    ?? eventData.OldValue?["name"]?.ToString(),
                ["source"] =
                    eventData.NewValue?["source"]?.ToString()
                    ?? eventData.OldValue?["source"]?.ToString(),
                ["target"] =
                    eventData.NewValue?["target"]?.ToString()
                    ?? eventData.OldValue?["target"]?.ToString(),
            };

        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = "Konnektr.DigitalTwins.Relationship.Lifecycle",
                DataContentType = "application/json",
                Subject = body["relationshipId"]?.ToString(),
                Time = eventData.Timestamp,
                // TraceParent = null,
            };

        return [cloudEvent];
    }

    public static List<CloudEvent> CreatePropertyEvents(EventData eventData, Uri source)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        List<CloudEvent> cloudEvents = [];
        JsonPatch jsonPatch = eventData.OldValue.CreatePatch(eventData.NewValue);

        foreach (PatchOperation op in jsonPatch.Operations)
        {
            if (op.Path.ToString().StartsWith("/$"))
            {
                continue;
            }
            string key = op.Path.ToString().Trim('/').Replace("/", "_");
            JsonObject body =
                new()
                {
                    ["timestamp"] = eventData.Timestamp,
                    ["serviceId"] = source.Host.ToString(),
                    ["id"] =
                        eventData.NewValue?["$dtId"]?.ToString()
                        ?? eventData.OldValue?["$dtId"]?.ToString(),
                    ["modelId"] =
                        eventData.NewValue?["modelId"]?.ToString()
                        ?? eventData.OldValue?["modelId"]?.ToString(),
                    ["key"] = key,
                    ["value"] = op.Value,
                    ["relationshipTarget"] =
                        eventData.NewValue?["target"]?.ToString()
                        ?? eventData.OldValue?["target"]?.ToString(),
                    ["relationshipId"] =
                        eventData.NewValue?["$relationshipId"]?.ToString()
                        ?? eventData.OldValue?["$relationshipId"]?.ToString(),
                    ["action"] = eventData.EventType.ToString(),
                };
            PatchOperation? sourceTimeOperation = jsonPatch.Operations.FirstOrDefault(o =>
                o.Path.ToString() == $"/$metadata/{key}/sourceTime"
            );
            if (sourceTimeOperation != null)
            {
                body["sourceTimestamp"] = sourceTimeOperation.Value;
            }

            CloudEvent cloudEvent =
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = source,
                    Data = body,
                    Type = "Konnektr.DigitalTwins.Property.Update",
                    DataContentType = "application/json",
                    Subject = body["id"]?.ToString(),
                    Time = eventData.Timestamp,
                    // TraceParent = null,
                };

            cloudEvents.Add(cloudEvent);
        }

        return cloudEvents;
    }

    #endregion
}
