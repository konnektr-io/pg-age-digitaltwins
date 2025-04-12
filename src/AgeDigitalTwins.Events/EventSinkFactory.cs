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
        if (kafkaSinks != null && kafkaSinks.Count > 0)
        {
            var logger = _loggerFactory.CreateLogger<KafkaEventSink>();
            foreach (var kafkaSink in kafkaSinks)
            {
                try
                {
                    sinks.Add(new KafkaEventSink(kafkaSink, new DefaultAzureCredential(), logger));
                }
                catch (ArgumentException ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to create Kafka event sink. Check the configuration for errors."
                    );
                }
            }
        }

        var mqttSinks = _configuration.GetSection("EventSinks:MQTT").Get<List<MqttSinkOptions>>();
        if (mqttSinks != null && mqttSinks.Count > 0)
        {
            var logger = _loggerFactory.CreateLogger<MqttEventSink>();
            foreach (var mqttSink in mqttSinks)
            {
                try
                {
                    sinks.Add(new MqttEventSink(mqttSink, logger));
                }
                catch (ArgumentException ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to create MQTT event sink. Check the configuration for errors."
                    );
                }
            }
        }

        var kustoSinks = _configuration
            .GetSection("EventSinks:Kusto")
            .Get<List<KustoSinkOptions>>();
        if (kustoSinks != null && kustoSinks.Count > 0)
        {
            var logger = _loggerFactory.CreateLogger<KustoEventSink>();
            foreach (var kustoSink in kustoSinks)
            {
                try
                {
                    sinks.Add(new KustoEventSink(kustoSink, new DefaultAzureCredential(), logger));
                }
                catch (ArgumentException ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to create Kusto event sink. Check the configuration for errors."
                    );
                }
            }
        }

        return sinks;
    }

    public List<EventRoute> GetEventRoutes()
    {
        return _configuration.GetSection("EventRoutes").Get<List<EventRoute>>() ?? [];
    }
}
