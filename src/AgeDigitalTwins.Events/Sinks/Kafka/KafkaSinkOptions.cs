using AgeDigitalTwins.Events.Abstractions;

namespace AgeDigitalTwins.Events.Sinks.Kafka;

public class KafkaSinkOptions : SinkOptions
{
    public required string BrokerList { get; set; }
    public required string Topic { get; set; }
    public string? SaslMechanism { get; set; } // Can be PLAIN or OAUTHBEARER
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
    public string? SecurityProtocol { get; set; } // Default to SaslSsl

    // OAuth
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TokenEndpoint { get; set; }
}
