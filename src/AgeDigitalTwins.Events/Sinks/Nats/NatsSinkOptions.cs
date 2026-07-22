using AgeDigitalTwins.Events.Abstractions;

namespace AgeDigitalTwins.Events.Sinks.Nats;

public class NatsSinkOptions : SinkOptions
{
    public required string Url { get; set; }
    public required string Subject { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
    public bool TlsEnabled { get; set; } = false;
    public bool TlsSkipVerify { get; set; } = false;
    public bool JetStream { get; set; } = false;
    public string? StreamName { get; set; }
    public int StreamReplicas { get; set; } = 1;
    public string StreamMaxAge { get; set; } = "72h";

    // OAuth
    public string? TokenEndpoint { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
