using AgeDigitalTwins.Events.Abstractions;

namespace AgeDigitalTwins.Events.Sinks.Webhook;

public class WebhookSinkOptions : SinkOptions
{
    public required string Url { get; set; }
    
    // Auth Types: "None", "Basic", "Bearer", "ApiKey"
    public string AuthenticationType { get; set; } = "None";

    // Basic Auth
    public string? Username { get; set; }
    public string? Password { get; set; }

    // Bearer Token
    public string? Token { get; set; }

    // API Key (Header)
    public string? HeaderName { get; set; }
    public string? HeaderValue { get; set; }
    
    // TODO: Implement OAuth (Client Credentials) support in the future
}
