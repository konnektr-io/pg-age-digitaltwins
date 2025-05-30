using System.Text.Json;
using System.Text.Json.Nodes;
using CloudNative.CloudEvents;
using Json.More;
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
                ["modelId"] = eventData.NewValue["$metadata"]?["$model"]?.DeepClone(),
                ["patch"] = JsonNode.Parse(jsonPatch.ToJsonDocument().RootElement.GetRawText()),
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
                // TODO: Figure out a way to retrieve the model id of the source twin, because this will be empty
                ["modelId"] = eventData.NewValue["$metadata"]?["$model"]?.DeepClone(),
                ["patch"] = JsonNode.Parse(jsonPatch.ToJsonDocument().RootElement.GetRawText()),
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

        cloudEvents.Add(cloudEvent);

        // Generate property events from the empty old valeu and new values
        cloudEvents.AddRange(CreateCloudEventsFromPatch(eventData, source));

        return cloudEvents;
    }

    public static List<CloudEvent> CreateRelationshipLifeCycleEvents(
        EventData eventData,
        Uri source
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
                // TODO: Figure out a way to retrieve the model id of the source twin
                // ["modelId"] =
                //     eventData.NewValue?["$metadata"]?["$model"]?.ToString()
                //     ?? eventData.OldValue?["$metadata"]?["$model"]?.ToString(),
            };

        CloudEvent cloudEvent =
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Source = source,
                Data = body,
                Type = "Konnektr.DigitalTwins.Relationship.Lifecycle",
                DataContentType = "application/json",
                Subject = $"{body["source"]}/relationships/{body["relationshipId"]}",
                Time = eventData.Timestamp,
                // TraceParent = null,
            };

        cloudEvents.Add(cloudEvent);

        // Generate property events from the empty old valeu and new values
        cloudEvents.AddRange(CreateCloudEventsFromPatch(eventData, source));

        return cloudEvents;
    }

    public static List<CloudEvent> CreatePropertyEvents(EventData eventData, Uri source)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        List<CloudEvent> cloudEvents = [];

        // Check for model changes
        // Create a lifecycle update event if the model has changed
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

            cloudEvents.Add(cloudEvent);
        }

        // Generate property events from the old and new values
        cloudEvents.AddRange(CreateCloudEventsFromPatch(eventData, source));

        return cloudEvents;
    }

    private static List<CloudEvent> CreateCloudEventsFromPatch(EventData eventData, Uri source)
    {
        JsonPatch jsonPatch = eventData.OldValue.CreatePatch(eventData.NewValue);
        List<CloudEvent> cloudEvents = [];
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
            // Add source time
            PatchOperation? sourceTimeOperation = jsonPatch.Operations.FirstOrDefault(o =>
                o.Path.ToString() == $"/$metadata/{key}/sourceTime"
            );
            if (sourceTimeOperation != null)
            {
                body["sourceTimeStamp"] = sourceTimeOperation.Value;
            }

            CloudEvent cloudEvent =
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Source = source,
                    Data = body,
                    Type = "Konnektr.DigitalTwins.Property.Event",
                    DataContentType = "application/json",
                    Subject = string.IsNullOrEmpty(body["relationshipId"]?.ToString())
                        // Twin property update
                        ? $"{body["id"]}"
                        // Relationship property update
                        : $"{body["id"]}/relationships/{body["relationshipId"]}",
                    Time = eventData.Timestamp,
                    // TraceParent = null,
                };

            cloudEvents.Add(cloudEvent);
        }
        return cloudEvents;
    }

    #endregion
}
