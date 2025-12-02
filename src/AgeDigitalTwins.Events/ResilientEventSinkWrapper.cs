using CloudNative.CloudEvents;

namespace AgeDigitalTwins.Events;

using System.Text.Json;
using Npgsql;

/// <summary>
/// Wraps an event sink with retry logic and exponential backoff for resilient event delivery.
/// </summary>
public class ResilientEventSinkWrapper(
    IEventSink innerSink,
    ILogger logger,
    DLQService dlqService,
    int maxRetries = 3,
    TimeSpan? initialDelay = null
) : IEventSink
{
    private readonly IEventSink _innerSink = innerSink;
    private readonly ILogger _logger = logger;
    private readonly int _maxRetries = maxRetries;
    private readonly TimeSpan _initialDelay = initialDelay ?? TimeSpan.FromSeconds(2);
    private readonly Queue<(
        List<CloudEvent> Events,
        DateTime FailedAt,
        int RetryCount
    )> _failedEventsQueue = new();
    private readonly SemaphoreSlim _queueLock = new(1, 1);

    private readonly DLQService _dlqService = dlqService;

    public string Name => _innerSink.Name;

    public bool IsHealthy => _innerSink.IsHealthy;

    public async Task SendEventsAsync(
        IEnumerable<CloudEvent> cloudEvents,
        CancellationToken cancellationToken = default
    )
    {
        var eventsList = cloudEvents.ToList();

        // First, try to send any previously failed events
        await RetryFailedEventsAsync(cancellationToken);

        // Now try to send the new events with retry logic
        await SendWithRetryAsync(eventsList, 0, cancellationToken);
    }

    private async Task SendWithRetryAsync(
        List<CloudEvent> events,
        int attemptNumber,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _innerSink.SendEventsAsync(events, cancellationToken);
        }
        catch (Exception ex)
        {
            if (attemptNumber < _maxRetries)
            {
                var delay = CalculateDelay(attemptNumber);
                _logger.LogWarning(
                    ex,
                    "Attempt {Attempt}/{MaxRetries} failed for sink {SinkName}. Retrying in {Delay}ms...",
                    attemptNumber + 1,
                    _maxRetries,
                    Name,
                    delay.TotalMilliseconds
                );

                await Task.Delay(delay, cancellationToken);
                await SendWithRetryAsync(events, attemptNumber + 1, cancellationToken);
            }
            else
            {
                _logger.LogError(
                    ex,
                    "All {MaxRetries} retry attempts failed for sink {SinkName}. Persisting {EventCount} events to DLQ.",
                    _maxRetries,
                    Name,
                    events.Count
                );

                // Persist each event to DLQ table
                foreach (var cloudEvent in events)
                {
                    await _dlqService.PersistEventAsync(
                        cloudEvent,
                        Name,
                        ex,
                        _maxRetries,
                        cancellationToken
                    );
                }
            }
        }
    }

    private async Task RetryFailedEventsAsync(CancellationToken cancellationToken)
    {
        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var toRetry = new List<(List<CloudEvent> Events, DateTime FailedAt, int RetryCount)>();

            // Dequeue all items to check
            while (_failedEventsQueue.Count > 0)
            {
                var item = _failedEventsQueue.Dequeue();
                var timeSinceFailure = now - item.FailedAt;
                var requiredDelay = CalculateDelay(item.RetryCount);

                if (timeSinceFailure >= requiredDelay)
                {
                    toRetry.Add(item);
                }
                else
                {
                    // Not ready yet, requeue
                    _failedEventsQueue.Enqueue(item);
                }
            }

            // Try to send the events that are ready for retry
            foreach (var (events, failedAt, retryCount) in toRetry)
            {
                try
                {
                    _logger.LogInformation(
                        "Retrying {EventCount} previously failed events for sink {SinkName} (retry #{Retry})",
                        events.Count,
                        Name,
                        retryCount + 1
                    );

                    await _innerSink.SendEventsAsync(events, cancellationToken);

                    _logger.LogInformation(
                        "Successfully sent {EventCount} previously failed events for sink {SinkName}",
                        events.Count,
                        Name
                    );
                }
                catch (Exception ex)
                {
                    if (retryCount < _maxRetries)
                    {
                        _logger.LogWarning(
                            ex,
                            "Retry attempt {Retry}/{MaxRetries} failed for sink {SinkName}. Will try again later.",
                            retryCount + 1,
                            _maxRetries,
                            Name
                        );
                        _failedEventsQueue.Enqueue((events, now, retryCount + 1));
                    }
                    else
                    {
                        _logger.LogError(
                            ex,
                            "Dropping {EventCount} events for sink {SinkName} after {MaxRetries} failed retries.",
                            events.Count,
                            Name,
                            _maxRetries
                        );
                    }
                }
            }
        }
        finally
        {
            _queueLock.Release();
        }
    }

    private TimeSpan CalculateDelay(int attemptNumber)
    {
        // Exponential backoff: 2s, 4s, 8s, 16s, ...
        var multiplier = Math.Pow(2, attemptNumber);
        var delay = TimeSpan.FromMilliseconds(_initialDelay.TotalMilliseconds * multiplier);

        // Cap at 60 seconds
        return delay > TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : delay;
    }

    public async Task<int> GetQueuedEventCountAsync()
    {
        await _queueLock.WaitAsync();
        try
        {
            return _failedEventsQueue.Sum(item => item.Events.Count);
        }
        finally
        {
            _queueLock.Release();
        }
    }
}
