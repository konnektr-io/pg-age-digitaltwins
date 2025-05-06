using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models;

/// <summary>
/// A page of results from a paginated query.
/// </summary>
/// <typeparam name="T"></typeparam>
public class Page<T>
{
    public IEnumerable<T> Value { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContinuationToken { get; set; }
}
