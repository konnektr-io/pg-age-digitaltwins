using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Events.Core.Auth;
using Azure.Core;
using Azure.Identity;
using Moq;
using Moq.Protected;
using Xunit;

namespace AgeDigitalTwins.Events.Test;

public class GenericClientCredentialTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsToken_WhenResponseIsValid()
    {
        // Arrange
        var tokenEndpoint = "https://identity.example.com/token";
        var clientId = "test-client";
        var clientSecret = "test-secret";
        var expectedAccessToken = "fake-access-token";
        
        var jsonResponse = $"{{\"access_token\": \"{expectedAccessToken}\", \"expires_in\": 3600, \"token_type\": \"Bearer\"}}";

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            // Verify request
            Assert.Equal(tokenEndpoint, request.RequestUri!.ToString());
            Assert.Equal(HttpMethod.Post, request.Method);
            
            var content = await request.Content!.ReadAsStringAsync();
            Assert.Contains("grant_type=client_credentials", content);
            Assert.Contains($"client_id={clientId}", content);
            Assert.Contains($"client_secret={clientSecret}", content);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
        });

        using var httpClient = new HttpClient(handler);
        var credential = new GenericClientCredential(tokenEndpoint, clientId, clientSecret, httpClient);
        var context = new TokenRequestContext(new[] { "custom-scope" });

        // Act
        var token = await credential.GetTokenAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expectedAccessToken, token.Token);
    }
}
