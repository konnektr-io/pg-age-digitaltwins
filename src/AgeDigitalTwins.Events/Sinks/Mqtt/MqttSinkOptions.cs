using AgeDigitalTwins.Events.Abstractions;
using MQTTnet.Formatter;

namespace AgeDigitalTwins.Events.Sinks.Mqtt;

public class MqttSinkOptions : SinkOptions
{
    public required string Broker { get; set; }
    public required int Port { get; set; }
    public required string Topic { get; set; }
    public required string ClientId { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    
    // OAuth
    public string? TokenEndpoint { get; set; }
    public string? TenantId { get; set; }
    public string? ClientSecret { get; set; }
    
    public string ProtocolVersion { get; set; } = "5.0.0";

    public MqttProtocolVersion GetProtocolVersion()
    {
        return ProtocolVersion switch
        {
            "3.1.0" => MqttProtocolVersion.V310,
            "3.1.1" => MqttProtocolVersion.V311,
            "5.0.0" => MqttProtocolVersion.V500,
            _ => MqttProtocolVersion.Unknown,
        };
    }
}
