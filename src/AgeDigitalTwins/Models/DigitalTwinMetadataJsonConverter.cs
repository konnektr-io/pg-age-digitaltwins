// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// Based on Azure.DigitalTwins.Core.DigitalTwinMetadataJsonConverter (MIT License)

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models;

/// <summary>
/// JSON converter to make it easier to deserialize a <see cref="DigitalTwinMetadata"/>.
/// </summary>
internal class DigitalTwinMetadataJsonConverter : JsonConverter<DigitalTwinMetadata>
{
    /// <inheritdoc/>
    public override DigitalTwinMetadata Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            throw new JsonException("Cannot deserialize null as DigitalTwinMetadata");
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(
                $"Unexpected token type {reader.TokenType} at index {reader.TokenStartIndex}. Expected JsonTokenType.StartObject."
            );
        }

        reader.Read(); // Advance into our object.
        var metadata = new DigitalTwinMetadata();

        // Until we reach the end of the object we began reading
        while (reader.TokenType != JsonTokenType.EndObject)
        {
            string? propertyName = reader.GetString();
            reader.Read(); // advance to the next token

            if (propertyName == DigitalTwinsJsonPropertyNames.MetadataModel)
            {
                metadata.ModelId =
                    JsonSerializer.Deserialize<string>(ref reader, options) ?? string.Empty;
            }
            else if (reader.TokenType == JsonTokenType.StartObject && propertyName != null)
            {
                var propertyMetadata = JsonSerializer.Deserialize<DigitalTwinPropertyMetadata>(
                    ref reader,
                    options
                );
                if (propertyMetadata != null)
                {
                    // Initialize PropertyMetadata dictionary if it's null
                    metadata.PropertyMetadata ??=
                        new Dictionary<string, DigitalTwinPropertyMetadata>();
                    metadata.PropertyMetadata[propertyName] = propertyMetadata;
                }
            }
            else if (propertyName != DigitalTwinsJsonPropertyNames.MetadataLastUpdateTime)
            {
                // Unexpected property
                throw new JsonException($"Unexpected property {propertyName} in metadata");
            }

            reader.Read(); // Finished consuming the token
        }

        return metadata;
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        DigitalTwinMetadata value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        writer.WriteString(DigitalTwinsJsonPropertyNames.MetadataModel, value.ModelId);

        if (value.PropertyMetadata != null)
        {
            foreach (KeyValuePair<string, DigitalTwinPropertyMetadata> p in value.PropertyMetadata)
            {
                writer.WritePropertyName(p.Key);
                JsonSerializer.Serialize<DigitalTwinPropertyMetadata>(writer, p.Value, options);
            }
        }

        writer.WriteEndObject();
    }
}
