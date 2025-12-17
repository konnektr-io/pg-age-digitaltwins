using System.Net.Http.Headers;
using System.Text;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using AgeDigitalTwins.Events.Abstractions;
using Azure.Core;

namespace AgeDigitalTwins.Events.Sinks.Webhook;

public class WebhookEventSink : IEventSink, IDisposable
{
    private readonly WebhookSinkOptions _options;
    private readonly TokenCredential? _credential;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly CloudEventFormatter _formatter = new JsonEventFormatter();
    private bool _isHealthy = true;

    public string Name => _options.Name;
    public bool IsHealthy => _isHealthy;

    public WebhookEventSink(WebhookSinkOptions options, TokenCredential? credential, ILogger logger, HttpClient? httpClient = null)
    {
        _options = options;
        _credential = credential;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
        
        ConfigureClient(); // Initial configuration
    }

    private void ConfigureClient()
    {
        if (_options.AuthenticationType == null)
            return;
        if (_options.AuthenticationType.Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{_options.Username}:{_options.Password}");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
        }
        else if (_options.AuthenticationType.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
             if (!string.IsNullOrEmpty(_options.Token))
             {
                 _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
             }
        }
        else if (_options.AuthenticationType.Equals("ApiKey", StringComparison.OrdinalIgnoreCase) || _options.AuthenticationType.Equals("Header", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(_options.HeaderName) && !string.IsNullOrEmpty(_options.HeaderValue))
            {
               if (_httpClient.DefaultRequestHeaders.Contains(_options.HeaderName))
                   _httpClient.DefaultRequestHeaders.Remove(_options.HeaderName);
               
               _httpClient.DefaultRequestHeaders.Add(_options.HeaderName, _options.HeaderValue);
            }
        }
    }

    public async Task SendEventsAsync(IEnumerable<CloudEvent> cloudEvents, CancellationToken cancellationToken = default)
    {
        bool useOAuth = string.Equals(_options.AuthenticationType, "OAuth", StringComparison.OrdinalIgnoreCase) && _credential != null;
        string? oauthToken = null;
        if (useOAuth && _credential != null)
        {
            try
            {
                // Use default scope if none provided (for generic OAuth, often not required, but Azure requires it)
                string[] scopes = Array.Empty<string>();
                if (!string.IsNullOrEmpty(_options.Scope))
                {
                    scopes = new[] { _options.Scope };
                }
                var tokenRequestContext = new Azure.Core.TokenRequestContext(scopes);
                var accessToken = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
                oauthToken = accessToken.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve OAuth token for Webhook sink '{SinkName}'", Name);
                throw;
            }
        }

        foreach (var cloudEvent in cloudEvents)
        {
            try
            {
                // Serialize event to JSON
                var bytes = _formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
                var content = new ByteArrayContent(bytes.ToArray());
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType.ToString());

                if (useOAuth && oauthToken != null)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, _options.Url)
                    {
                        Content = content
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _isHealthy = false;
                        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogError("Webhook sink '{SinkName}' failed to send event {EventId}. Status: {StatusCode}, Response: {Response}",
                            Name, cloudEvent.Id, response.StatusCode, responseBody);
                    }
                    else
                    {
                        _isHealthy = true;
                        _logger.LogDebug("Webhook sink '{SinkName}' successfully sent event {EventId}", Name, cloudEvent.Id);
                    }
                }
                else
                {
                    // All other auth types use the preconfigured HttpClient
                    var response = await _httpClient.PostAsync(_options.Url, content, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _isHealthy = false;
                        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogError("Webhook sink '{SinkName}' failed to send event {EventId}. Status: {StatusCode}, Response: {Response}",
                            Name, cloudEvent.Id, response.StatusCode, responseBody);
                    }
                    else
                    {
                        _isHealthy = true;
                        _logger.LogDebug("Webhook sink '{SinkName}' successfully sent event {EventId}", Name, cloudEvent.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _isHealthy = false;
                _logger.LogError(ex, "Error sending event {EventId} to webhook sink '{SinkName}'", cloudEvent.Id, Name);
                throw;
            }
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
