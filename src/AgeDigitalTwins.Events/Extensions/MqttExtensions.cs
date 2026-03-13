// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using CloudNative.CloudEvents.Core;
using MQTTnet;

namespace CloudNative.CloudEvents.Mqtt;

/// <summary>
/// Extension methods to convert between CloudEvents and MQTT messages.
/// </summary>
public static class MqttExtensions
{
    /// <summary>
    /// Converts this MQTT message into a CloudEvent object.
    /// </summary>
    /// <param name="message">The MQTT message to convert. Must not be null.</param>
    /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
    /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
    /// <returns>A reference to a validated CloudEvent instance.</returns>
    public static CloudEvent ToCloudEvent(
        this MqttApplicationMessage message,
        CloudEventFormatter formatter,
        params CloudEventAttribute[]? extensionAttributes
    ) => ToCloudEvent(message, formatter, (IEnumerable<CloudEventAttribute>?)extensionAttributes);

    /// <summary>
    /// Converts this MQTT message into a CloudEvent object.
    /// </summary>
    /// <param name="message">The MQTT message to convert. Must not be null.</param>
    /// <param name="formatter">The event formatter to use to parse the CloudEvent. Must not be null.</param>
    /// <param name="extensionAttributes">The extension attributes to use when parsing the CloudEvent. May be null.</param>
    /// <returns>A reference to a validated CloudEvent instance.</returns>
    public static CloudEvent ToCloudEvent(
        this MqttApplicationMessage message,
        CloudEventFormatter formatter,
        IEnumerable<CloudEventAttribute>? extensionAttributes
    )
    {
        Validation.CheckNotNull(formatter, nameof(formatter));
        Validation.CheckNotNull(message, nameof(message));

        // TODO: Determine if there's a sensible content type we should apply.

        // Convert ReadOnlySequence<byte> to a Stream
        using var stream = new MemoryStream();
        foreach (var segment in message.Payload)
        {
            stream.Write(segment.Span);
        }
        stream.Position = 0;
        return formatter.DecodeStructuredModeMessage(
            stream,
            contentType: null,
            extensionAttributes
        );
    }

    // TODO: Support both binary and structured mode.
    /// <summary>
    /// Converts a CloudEvent to <see cref="MqttApplicationMessage"/>.
    /// </summary>
    /// <param name="cloudEvent">The CloudEvent to convert. Must not be null, and must be a valid CloudEvent.</param>
    /// <param name="contentMode">Content mode. Currently only structured mode is supported.</param>
    /// <param name="formatter">The formatter to use within the conversion. Must not be null.</param>
    /// <param name="topic">The MQTT topic for the message. May be null.</param>
    public static MqttApplicationMessage ToMqttApplicationMessage(
        this CloudEvent cloudEvent,
        ContentMode contentMode,
        CloudEventFormatter formatter,
        string? topic
    )
    {
        Validation.CheckCloudEventArgument(cloudEvent, nameof(cloudEvent));
        Validation.CheckNotNull(formatter, nameof(formatter));

        return contentMode switch
        {
            ContentMode.Structured => new MqttApplicationMessage
            {
                Topic = topic,
                PayloadSegment = BinaryDataUtilities.GetArraySegment(
                    formatter.EncodeStructuredModeMessage(cloudEvent, out _)
                ),
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(contentMode),
                $"Unsupported content mode: {contentMode}"
            ),
        };
    }
}
