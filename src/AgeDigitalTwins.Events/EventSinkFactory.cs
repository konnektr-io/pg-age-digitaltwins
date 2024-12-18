using Azure.Identity;

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

        var mqttSinks = _configuration.GetSection("EventSinks:MQTT").Get<List<MqttSinkOptions>>();
        if (mqttSinks != null)
        {
            foreach (var mqttSink in mqttSinks)
            {
                var logger = _loggerFactory.CreateLogger<MqttEventSink>();
                sinks.Add(new MqttEventSink(mqttSink, logger));
            }
        }

        var kustoSinks = _configuration
            .GetSection("EventSinks:Kusto")
            .Get<List<KustoSinkOptions>>();
        if (kustoSinks != null)
        {
            foreach (var kustoSink in kustoSinks)
            {
                var logger = _loggerFactory.CreateLogger<KustoEventSink>();
                sinks.Add(new KustoEventSink(kustoSink, new DefaultAzureCredential(), logger));
            }
        }

        return sinks;
    }

    public List<EventRoute> GetEventRoutes()
    {
        return _configuration.GetSection("EventRoutes").Get<List<EventRoute>>() ?? [];
    }
}
