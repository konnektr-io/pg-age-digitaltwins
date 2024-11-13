// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Serialization
{
    // TODO: Remove when #16272 is fixed
    internal class OptionalETagConverter : JsonConverter<EntityTagHeaderValue?>
    {
        public override EntityTagHeaderValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? value = reader.GetString();
            return value != null
                ? new EntityTagHeaderValue(value)
                : (EntityTagHeaderValue?)null;
        }

        public override void Write(Utf8JsonWriter writer, EntityTagHeaderValue? value, JsonSerializerOptions options)
        {
            if (value == null)
            { writer.WriteNullValue(); }
            else
            { writer.WriteStringValue(value.ToString()); }
        }
    }
}