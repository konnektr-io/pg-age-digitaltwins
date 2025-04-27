using System;
using System.Collections.Generic;

namespace AgeDigitalTwins.Models;

public class Page<T>
{
    public IEnumerable<T> Value { get; set; } = [];
    public ContinuationToken? ContinuationToken { get; set; }
}
