using System;
using System.Collections.Generic;

namespace AgeDigitalTwins.Models;

public class Page<T>
{
    public IEnumerable<T> Values { get; set; } = [];
    public ContinuationToken? ContinuationToken { get; set; }
}
