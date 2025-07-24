using AgeDigitalTwins.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.Events.Test;

/// <summary>
/// Event sink factory for tests that creates only the test sink and ignores configuration.
/// </summary>
public class TestingEventSinkFactory : EventSinkFactory
{
    private readonly TestingEventSink _testSink;

    public TestingEventSinkFactory(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        TestingEventSink testSink
    )
        : base(configuration, loggerFactory)
    {
        _testSink = testSink;
    }

    public override List<IEventSink> CreateEventSinks()
    {
        // Return only the test sink, ignore configuration
        return new List<IEventSink> { _testSink };
    }

    public override List<EventRoute> GetEventRoutes()
    {
        // Return a single route that sends all events to our test sink
        return new List<EventRoute>
        {
            new EventRoute
            {
                SinkName = _testSink.Name,
                EventFormat =
                    EventFormat.EventNotification // Using EventNotification format for simpler testing
                ,
            },
        };
    }

    /// <summary>
    /// Gets the test sink for assertions.
    /// </summary>
    public TestingEventSink GetTestSink() => _testSink;
}
