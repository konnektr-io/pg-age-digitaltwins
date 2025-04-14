using System.Collections.Generic;

namespace AgeDigitalTwins.Models;

public class Page<T>
{
    public ContinuationToken? ContinuationToken { get; set; }
    public IEnumerable<T> Values { get; set; } = [];
}
