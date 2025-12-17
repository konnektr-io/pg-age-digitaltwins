using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events.Abstractions;

public interface IEventSink
{
    string Name { get; }

    /// <summary>
    /// Indicates whether the event sink is healthy and able to send events.
    /// </summary>
    bool IsHealthy { get; }

    Task SendEventsAsync(
        IEnumerable<CloudEvent> cloudEvents,
        CancellationToken cancellationToken = default
    );
}
