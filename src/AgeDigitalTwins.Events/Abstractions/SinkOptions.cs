namespace AgeDigitalTwins.Events.Abstractions;

public abstract class SinkOptions
{
    public required string Name { get; set; }

    // "None", "Basic", "ConnectionString", "ManagedIdentity", "OAuth"
    public string? AuthenticationType { get; set; }
    
    public string? Scope { get; set; }

    public Dictionary<SinkEventType, string>? EventTypeMappings { get; set; }
}
