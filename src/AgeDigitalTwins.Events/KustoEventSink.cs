using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public class KustoEventSink(KustoSinkOptions options) : IEventSink
{
    private readonly KustoSinkOptions _options = options;

    public string Name => _options.Name;

    public async Task SendEventsAsync(IEnumerable<CloudEvent> cloudEvents)
    {
        await Task.CompletedTask;
        // Implement Kusto sending logic here
    }
}

public class KustoSinkOptions
{
    public required string Name { get; set; }
    public required string ClusterUrl { get; set; }
    public required string Database { get; set; }
    public required KustoIngestionType IngestionType { get; set; } = KustoIngestionType.Queued;
}

public enum KustoIngestionType
{
    Queued,
    Streaming,
}
