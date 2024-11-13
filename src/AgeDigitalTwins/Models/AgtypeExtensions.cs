using System;
using System.Text.Json;
using Npgsql.Age.Types;

namespace AgeDigitalTwins.Models
{
    public static class AgtypeExtensions
    {
        /// <summary>
        /// Return the agtype value as a <see cref="BasicDigitalTwin"/>.
        /// </summary>
        /// <returns>
        /// Vertex.
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown when the agtype cannot be converted to a vertex.
        /// </exception>
        public static BasicDigitalTwin? GetBasicDigitalTwin(this Vertex vertex)
        {
            string serializedProperties = JsonSerializer.Serialize(vertex.Properties);
            return JsonSerializer.Deserialize<BasicDigitalTwin>(serializedProperties);
        }
    }
}