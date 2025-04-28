using System;
using System.Collections.Generic;

namespace AgeDigitalTwins.Models;

/// <summary>
/// A page of results from a paginated query.
/// </summary>
/// <typeparam name="T"></typeparam>
public class Page<T>
{
    public IEnumerable<T> Value { get; set; } = [];
    public ContinuationToken? ContinuationToken { get; set; }
}
