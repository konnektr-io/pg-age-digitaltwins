namespace AgeDigitalTwins.Events;

public class EventSinkFactory(IConfiguration configuration, ILogger<EventSinkFactory> logger)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<EventSinkFactory> _logger = logger;

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
                sinks.Add(new KafkaEventSink(kafkaSink, _logger));
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
