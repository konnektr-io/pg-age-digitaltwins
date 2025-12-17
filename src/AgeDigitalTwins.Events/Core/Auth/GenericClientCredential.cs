using System.Text.Json;
using System.Threading;
using System.Net.Http;
using Azure.Core;
using Azure.Identity;

namespace AgeDigitalTwins.Events.Core.Auth;

/// <summary>
/// A TokenCredential that authenticates via a generic OAuth 2.0 Client Credentials flow.
/// </summary>
public class GenericClientCredential : TokenCredential
{
    private readonly string _tokenEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly HttpClient _httpClient;
    
    private AccessToken? _cachedToken;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public GenericClientCredential(string tokenEndpoint, string clientId, string clientSecret, HttpClient? httpClient = null)
    {
        _tokenEndpoint = tokenEndpoint ?? throw new ArgumentNullException(nameof(tokenEndpoint));
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        _httpClient = httpClient ?? new HttpClient();
    }

    public override AccessToken GetToken(TokenRequestContext context, CancellationToken cancellationToken)
    {
        return GetTokenAsync(context, cancellationToken).GetAwaiter().GetResult();
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken cancellationToken)
    {
        // Check cache
        if (_cachedToken.HasValue && _cachedToken.Value.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _cachedToken.Value;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache
            if (_cachedToken.HasValue && _cachedToken.Value.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return _cachedToken.Value;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            var keyValues = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "client_credentials"),
                new("client_id", _clientId),
                new("client_secret", _clientSecret)
            };

            if (context.Scopes != null && context.Scopes.Length > 0)
            {
                 // Join scopes with space
                 keyValues.Add(new("scope", string.Join(" ", context.Scopes)));
            }
            
            request.Content = new FormUrlEncodedContent(keyValues);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var accessTokenProp))
            {
                var accessToken = accessTokenProp.GetString()!;
                var expiresIn = root.TryGetProperty("expires_in", out var expiresInProp) ? expiresInProp.GetInt32() : 3600;
                
                var expiresOn = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
                
                _cachedToken = new AccessToken(accessToken, expiresOn);
                return _cachedToken.Value;
            }
            
            throw new AuthenticationFailedException("Token endpoint did not return an access_token.");
        }
        catch (Exception ex)
        {
             throw new AuthenticationFailedException($"Failed to retrieve token from {_tokenEndpoint}: {ex.Message}", ex);
        }
        finally
        {
            _lock.Release();
        }
    }
}
