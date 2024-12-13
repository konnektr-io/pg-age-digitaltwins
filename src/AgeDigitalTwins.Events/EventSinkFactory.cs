namespace AgeDigitalTwins.Events;

public class EventSinkFactory(IConfiguration configuration, ILoggerFactory loggerFactory)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public List<IEventSink> CreateEventSinks()
    {
        var sinks = new List<IEventSink>();

        var kafkaSinks = _configuration
            .GetSection("EventSinks:Kafka")
            .Get<List<KafkaSinkOptions>>();
        if (kafkaSinks != null)
        {
            foreach (var kafkaSink in kafkaSinks)
            {
                var logger = _loggerFactory.CreateLogger<KafkaEventSink>();
                sinks.Add(new KafkaEventSink(kafkaSink, logger));
            }
        }

        /* var kustoSinks = _configuration
            .GetSection("EventSinks:Kusto")
            .Get<List<KustoSinkOptions>>();
        foreach (var kustoSink in kustoSinks)
        {
            sinks.Add(new KustoEventSink(kustoSink));
        } */

        /* var mqttSinks = _configuration.GetSection("EventSinks:MQTT").Get<List<MqttSinkOptions>>();
        foreach (var mqttSink in mqttSinks)
        {
            sinks.Add(new MqttEventSink(mqttSink));
        } */

        return sinks;
    }

    public List<EventRoute> GetEventRoutes()
    {
        return _configuration.GetSection("EventRoutes").Get<List<EventRoute>>() ?? [];
    }
}
