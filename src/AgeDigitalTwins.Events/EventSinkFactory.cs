namespace AgeDigitalTwins.Events;

public class EventSinkFactory
{
    public static List<IEventSink> CreateEventSinks()
    {
        var sinks = new List<IEventSink>();

        if (Environment.GetEnvironmentVariable("USE_KAFKA") == "true")
        {
            sinks.Add(new KafkaEventSink());
        }

        if (Environment.GetEnvironmentVariable("USE_KUSTO") == "true")
        {
            sinks.Add(new KustoEventSink());
        }

        if (Environment.GetEnvironmentVariable("USE_MQTT") == "true")
        {
            sinks.Add(new MqttEventSink());
        }

        return sinks;
    }
}
