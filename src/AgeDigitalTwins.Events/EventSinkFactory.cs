using Azure.Identity;

namespace AgeDigitalTwins.Events;

public class EventSinkFactory(
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    DLQService dlqService
)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly DLQService _dlqService = dlqService;

    public virtual List<IEventSink> CreateEventSinks()
    {
        var sinks = new List<IEventSink>();
        var wrapperLogger = _loggerFactory.CreateLogger<ResilientEventSinkWrapper>();

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
                    var sink = new KafkaEventSink(kafkaSink, new DefaultAzureCredential(), logger);
                    // Wrap with resilient wrapper for retry logic
                    sinks.Add(new ResilientEventSinkWrapper(sink, wrapperLogger, _dlqService));
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
                    var sink = new MqttEventSink(mqttSink, logger);
                    // Wrap with resilient wrapper for retry logic
                    sinks.Add(new ResilientEventSinkWrapper(sink, wrapperLogger, _dlqService));
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
                    var sink = new KustoEventSink(kustoSink, new DefaultAzureCredential(), logger);
                    // Wrap with resilient wrapper for retry logic
                    sinks.Add(new ResilientEventSinkWrapper(sink, wrapperLogger, _dlqService));
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

    public virtual List<EventRoute> GetEventRoutes()
    {
        return _configuration.GetSection("EventRoutes").Get<List<EventRoute>>() ?? [];
    }
}
