using System.Text.Json;
using System.Text.Json.Serialization;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.Events;

/// <summary>
/// Shared event consumer that processes events from the shared queue
/// and forwards them to configured sinks based on routing rules.
/// </summary>
public class SharedEventConsumer
{
    private readonly IEventQueue _eventQueue;
    private readonly ILogger<SharedEventConsumer> _logger;
    private readonly Uri _sourceUri;
    private readonly Timer _batchTimer;
    private readonly int _batchSize;
    private readonly TimeSpan _batchInterval;

    // Event processing metrics
    private long _totalEventsProcessed = 0;
    private DateTime _lastProcessedAt = DateTime.UtcNow;

    public SharedEventConsumer(
        IEventQueue eventQueue,
        ILogger<SharedEventConsumer> logger,
        Uri sourceUri,
        int batchSize = 100,
        TimeSpan? batchInterval = null)
    {
        _eventQueue = eventQueue;
        _logger = logger;
        _sourceUri = sourceUri;
        _batchSize = batchSize;
        _batchInterval = batchInterval ?? TimeSpan.FromSeconds(5);

        // Set up batch processing timer
        _batchTimer = new Timer(TriggerBatchProcessing, null, _batchInterval, _batchInterval);
    }

    /// <summary>
    /// JSON serializer options for event processing
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Gets the current queue depth (number of events waiting to be processed)
    /// </summary>
    public int QueueDepth => _eventQueue.Count;

    /// <summary>
    /// Gets the total number of events processed since startup
    /// </summary>
    public long TotalEventsProcessed => _totalEventsProcessed;

    /// <summary>
    /// Gets the total number of events enqueued since startup
    /// </summary>
    public long TotalEventsEnqueued => _eventQueue.TotalEnqueued;

    /// <summary>
    /// Gets when events were last processed
    /// </summary>
    public DateTime LastProcessedAt => _lastProcessedAt;

    /// <summary>
    /// Process events in batches and send to configured sinks
    /// </summary>
    public async Task ConsumeEventsAsync(
        List<IEventSink> eventSinks,
        List<EventRoute> eventRoutes,
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(eventSinks, eventRoutes, cancellationToken);
                await Task.Delay(100, cancellationToken); // Small delay between batch checks
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event batch");
                await Task.Delay(1000, cancellationToken); // Back off on error
            }
        }
    }

    /// <summary>
    /// Process a single batch of events
    /// </summary>
    private async Task ProcessBatchAsync(
        List<IEventSink> eventSinks,
        List<EventRoute> eventRoutes,
        CancellationToken cancellationToken = default)
    {
        var batch = _eventQueue.DequeueBatch(_batchSize);
        if (batch.Count == 0)
            return;

        _logger.LogDebug("Processing batch of {BatchSize} events", batch.Count);

        try
        {
            await ProcessEventDataBatchAsync(batch, eventSinks, eventRoutes);

            Interlocked.Add(ref _totalEventsProcessed, batch.Count);
            _lastProcessedAt = DateTime.UtcNow;

            _logger.LogDebug("Successfully processed batch of {BatchSize} events", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event batch of {BatchSize} events", batch.Count);
            throw;
        }
    }

    /// <summary>
    /// Process a batch of event data and route to sinks
    /// </summary>
    private async Task ProcessEventDataBatchAsync(
        List<EventData> eventDataBatch,
        List<IEventSink> eventSinks,
        List<EventRoute> eventRoutes)
    {
        // Group events by sink to optimize delivery
        var sinkEventGroups = new Dictionary<IEventSink, List<CloudEvent>>();

        foreach (var eventData in eventDataBatch)
        {
            // Find matching routes for this event
            var matchingRoutes = eventRoutes.Where(route => ShouldRouteEvent(route, eventData)).ToList();

            foreach (var route in matchingRoutes)
            {
                // Find the sink for this route
                var sink = eventSinks.FirstOrDefault(s => s.Name == route.SinkName);
                if (sink == null)
                {
                    _logger.LogWarning("Sink {SinkName} not found for event route", route.SinkName);
                    continue;
                }

                // Generate cloud events for this route
                var cloudEvents = route.EventFormat switch
                {
                    EventFormat.EventNotification => CloudEventFactory.CreateEventNotificationEvents(
                        eventData,
                        _sourceUri,
                        route.TypeMappings
                    ),
                    EventFormat.DataHistory => CloudEventFactory.CreateDataHistoryEvents(
                        eventData,
                        _sourceUri,
                        route.TypeMappings
                    ),
                    EventFormat.Telemetry => CloudEventFactory.CreateTelemetryEvents(
                        eventData,
                        _sourceUri,
                        route.TypeMappings
                    ),
                    _ => throw new ArgumentException($"Unknown route event format: {route.EventFormat}")
                };

                if (cloudEvents.Count == 0)
                    continue;

                // Add events to the sink's batch
                if (!sinkEventGroups.ContainsKey(sink))
                {
                    sinkEventGroups[sink] = new List<CloudEvent>();
                }
                sinkEventGroups[sink].AddRange(cloudEvents);
            }
        }

        // Send batched events to each sink
        var sinkTasks = sinkEventGroups.Select(async kvp =>
        {
            var sink = kvp.Key;
            var events = kvp.Value;

            try
            {
                _logger.LogDebug("Sending {EventCount} events to sink {SinkName}", events.Count, sink.Name);
                await sink.SendEventsAsync(events);
                _logger.LogDebug("Successfully sent {EventCount} events to sink {SinkName}", events.Count, sink.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send {EventCount} events to sink {SinkName}", events.Count, sink.Name);
                throw;
            }
        });

        await Task.WhenAll(sinkTasks);
    }

    /// <summary>
    /// Determine if an event should be routed based on the route configuration
    /// </summary>
    private static bool ShouldRouteEvent(EventRoute route, EventData eventData)
    {
        // For now, route all events. In the future, we can add filtering logic here
        // based on event type, twin ID patterns, etc.
        return true;
    }

    /// <summary>
    /// Timer callback to trigger batch processing
    /// </summary>
    private void TriggerBatchProcessing(object? state)
    {
        // This method is called by the timer to ensure events are processed
        // even if the batch size threshold isn't reached
        // The actual processing happens in ConsumeEventsAsync
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        _batchTimer?.Dispose();
    }
}
