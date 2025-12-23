using AgeDigitalTwins.Events.Abstractions;

namespace AgeDigitalTwins.Events.Sinks.Kusto;

public class KustoSinkOptions : SinkOptions
{
    public required string IngestionUri { get; set; }
    public required string Database { get; set; }
    public string? PropertyEventsTable { get; set; }
    public string? TwinLifeCycleEventsTable { get; set; }
    public string? RelationshipLifeCycleEventsTable { get; set; }

    // Auth
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
