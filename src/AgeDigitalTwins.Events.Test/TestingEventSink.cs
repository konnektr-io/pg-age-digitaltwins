using System.Collections.Concurrent;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.Events.Test;

/// <summary>
/// Test event sink that captures events in memory for verification in integration tests.
/// </summary>
public class TestingEventSink : IEventSink
{
    private readonly ConcurrentQueue<CloudEvent> _capturedEvents = new();
    private readonly ILogger<TestingEventSink> _logger;

    public string Name { get; }

    public TestingEventSink(string name, ILogger<TestingEventSink> logger)
    {
        Name = name;
        _logger = logger;
    }

    public Task SendEventsAsync(IEnumerable<CloudEvent> events)
    {
        foreach (var evt in events)
        {
            _capturedEvents.Enqueue(evt);
            _logger.LogDebug("Captured event: {EventType} for {Subject}", evt.Type, evt.Subject);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all captured events as an array.
    /// </summary>
    public IEnumerable<CloudEvent> GetCapturedEvents() => _capturedEvents.ToArray();

    /// <summary>
    /// Waits for a specific event to be received based on subject and event type.
    /// </summary>
    /// <param name="expectedSubject">The expected subject (e.g., twin ID)</param>
    /// <param name="expectedEventType">The expected event type</param>
    /// <param name="timeout">Maximum time to wait</param>
    /// <returns>The matching event or null if not found within timeout</returns>
    public CloudEvent? WaitForEvent(
        string expectedSubject,
        string expectedEventType,
        TimeSpan timeout
    )
    {
        var endTime = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTime)
        {
            var events = GetCapturedEvents();
            var matchingEvent = events.FirstOrDefault(e =>
                e.Subject == expectedSubject && e.Type == expectedEventType
            );
            if (matchingEvent != null)
                return matchingEvent;

            Thread.Sleep(100); // Poll every 100ms
        }
        return null;
    }

    /// <summary>
    /// Clears all captured events.
    /// </summary>
    public void ClearEvents() => _capturedEvents.Clear();

    /// <summary>
    /// Gets the count of captured events.
    /// </summary>
    public int EventCount => _capturedEvents.Count;
}
