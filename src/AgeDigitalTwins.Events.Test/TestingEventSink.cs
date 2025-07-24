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
    public async Task<CloudEvent?> WaitForEventAsync(
        string expectedSubject,
        string expectedEventType,
        TimeSpan timeout
    )
    {
        var endTime = DateTime.UtcNow.Add(timeout);
        var checkCount = 0;

        _logger.LogInformation(
            "Starting to wait for event: {EventType} with subject: {Subject}",
            expectedEventType,
            expectedSubject
        );
        Console.WriteLine(
            $"TestingEventSink: Waiting for event {expectedEventType} with subject {expectedSubject}"
        );

        while (DateTime.UtcNow < endTime)
        {
            checkCount++;
            var events = GetCapturedEvents();

            if (checkCount % 50 == 0) // Log every 5 seconds
            {
                _logger.LogInformation(
                    "Wait check #{Count}: Have {EventCount} total events",
                    checkCount,
                    events.Count()
                );
                Console.WriteLine(
                    $"TestingEventSink: Check #{checkCount}, have {events.Count()} total events"
                );

                if (events.Any())
                {
                    Console.WriteLine("Current events:");
                    foreach (var evt in events.Take(5)) // Show first 5 events
                    {
                        Console.WriteLine($"  - {evt.Type} for {evt.Subject}");
                    }
                }
            }

            var matchingEvent = events.FirstOrDefault(e =>
                e.Subject == expectedSubject && e.Type == expectedEventType
            );
            if (matchingEvent != null)
            {
                _logger.LogInformation(
                    "Found matching event after {CheckCount} checks",
                    checkCount
                );
                Console.WriteLine(
                    $"TestingEventSink: Found matching event after {checkCount} checks"
                );
                return matchingEvent;
            }

            await Task.Delay(100); // Poll every 100ms
        }

        _logger.LogWarning(
            "Timeout waiting for event {EventType} with subject {Subject} after {CheckCount} checks",
            expectedEventType,
            expectedSubject,
            checkCount
        );
        Console.WriteLine(
            $"TestingEventSink: Timeout waiting for event {expectedEventType} with subject {expectedSubject} after {checkCount} checks"
        );
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
