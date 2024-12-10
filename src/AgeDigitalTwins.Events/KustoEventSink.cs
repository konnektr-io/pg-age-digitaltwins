using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

public class KustoEventSink : IEventSink
{
    public async Task SendEventAsync(CloudEvent cloudEvent)
    {
        // Implement Kusto sending logic here
    }
}
