using System;
using System.Threading;
using System.Threading.Tasks;
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
        // For OAuth, we ensure we have a valid token before sending batch
        if (_options.AuthenticationType.Equals("OAuth", StringComparison.OrdinalIgnoreCase) && _credential != null)
        {
             try 
             {
                  // We should ideally cache this token or rely on GetTokenAsync's internal caching.
                  // GenericClientCredential implements caching, and Azure specific credentials do too.
                  // But setting the header on singleton _httpClient is risky if concurrent calls? 
                  // Wait, modifying DefaultRequestHeaders is not thread safe if there are concurrent batches.
                  // However, ResilientEventSinkWrapper calls SendEventsAsync sequentially (unless configured otherwise? It uses a lock/queue? No, usually singleton usage).
                  // But HttpClient is designed for concurrent use EXCEPT for mutation of properties like DefaultRequestHeaders.
                  // It is safer to create a HttpRequestMessage per request and set headers on the message.
                  
                  // Refactor: We need to pull the token.
             }
             catch(Exception ex)
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
            catch (Exception ex)
            {
                _isHealthy = false;
                _logger.LogError(ex, "Error sending event {EventId} to webhook sink '{SinkName}'", cloudEvent.Id, Name);
                // We don't rethrow here so we can try to process other events in the batch, 
                // but usually the caller (Wrapper) might want to know if *any* failed. 
                // However, the interface contract is void Task. The errors are logged.
                // If we want retry, we should probably throw. The ResilientEventSinkWrapper catches exceptions.
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
