using System;
using System.Text.Json;
using Npgsql.Age.Types;

namespace AgeDigitalTwins.Models
{
    public static class AgtypeExtensions
    {
        /// <summary>
        /// Return the agtype value properties as a <see cref="JsonElement"/>.
        /// </summary>
        /// <returns>
        /// Vertex.
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown when the agtype cannot be converted to a vertex.
        /// </exception>
        public static JsonObject? GetPropertiesJson(this Agtype agtype)
        {
            bool isValidEdge = _value.EndsWith(Edge.FOOTER);
            bool isValidVertex = _value.EndsWith(Vertex.FOOTER);
            if (!isValidEdge && !isValidVertex)
                throw new FormatException("Cannot convert agtype to edge or vertex. Agtype is not a valid edge or vertex.");

            var jsonString = _value
                .Replace(Edge.FOOTER, "")
                .Replace(Vertex.FOOTER, "")
                .Trim('\u0001');

            var json = JsonSerializer.Deserialize<JsonElement>(jsonString, SerializerOptions.Default);

            if (json.TryGetProperty("properties", out JsonElement properties))
            {
                return properties;
            }

            throw new FormatException("The agtype does not contain a 'properties' object.");
        }
    }
}