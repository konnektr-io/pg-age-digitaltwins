using System.Collections.Concurrent;

namespace AgeDigitalTwins.Events;

/// <summary>
/// Interface for a shared event queue that can be used by multiple event producers
/// </summary>
public interface IEventQueue
{
    /// <summary>
    /// Enqueue an event for processing
    /// </summary>
    void Enqueue(EventData eventData);

    /// <summary>
    /// Try to dequeue an event for processing
    /// </summary>
    bool TryDequeue(out EventData? eventData);

    /// <summary>
    /// Try to dequeue a batch of events up to the specified count
    /// </summary>
    List<EventData> DequeueBatch(int maxCount);

    /// <summary>
    /// Gets the current queue depth (number of events waiting to be processed)
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the total number of events enqueued since startup
    /// </summary>
    long TotalEnqueued { get; }
}

/// <summary>
/// Singleton implementation of IEventQueue using ConcurrentQueue for thread safety
/// </summary>
public class EventQueue : IEventQueue
{
    private readonly ConcurrentQueue<EventData> _queue = new();
    private long _totalEnqueued = 0;

    public int Count => _queue.Count;
    public long TotalEnqueued => _totalEnqueued;

    public void Enqueue(EventData eventData)
    {
        _queue.Enqueue(eventData);
        Interlocked.Increment(ref _totalEnqueued);
    }

    public bool TryDequeue(out EventData? eventData)
    {
        return _queue.TryDequeue(out eventData);
    }

    public List<EventData> DequeueBatch(int maxCount)
    {
        var batch = new List<EventData>();
        for (int i = 0; i < maxCount && _queue.TryDequeue(out var eventData); i++)
        {
            batch.Add(eventData);
        }
        return batch;
    }
}
