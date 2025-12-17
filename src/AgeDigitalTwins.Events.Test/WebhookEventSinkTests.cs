using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Events.Sinks.Webhook;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AgeDigitalTwins.Events.Test;

public class WebhookEventSinkTests
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
    public async Task SendEventsAsync_IncludesBasicAuthHeader_WhenConfigured()
    {
        // Arrange
        var options = new WebhookSinkOptions
        {
            Name = "TestSink",
            Url = "https://example.com/webhook",
            AuthenticationType = "Basic",
            Username = "user",
            Password = "password"
        };
        var loggerMock = new Mock<ILogger>();
        
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            Assert.Equal(options.Url, request.RequestUri!.ToString());
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Basic", request.Headers.Authorization.Scheme);
            // "user:password" -> base64 "dXNlcjpwYXNzd29yZA=="
            Assert.Equal("dXNlcjpwYXNzd29yZA==", request.Headers.Authorization.Parameter);
            
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var sink = new WebhookEventSink(options, null, loggerMock.Object, httpClient);
        
        var cloudEvent = new CloudEvent
        {
            Id = "123",
            Type = "TestEvent",
            Source = new Uri("urn:test"),
            Time = DateTimeOffset.UtcNow,
            Data = "test-data"
        };

        // Act
        await sink.SendEventsAsync(new[] { cloudEvent });

        // Assert - implicit inside the handler
    }

    [Fact]
    public async Task SendEventsAsync_IncludesBearerHeader_WhenConfigured()
    {
        // Arrange
        var options = new WebhookSinkOptions
        {
            Name = "TestSink",
            Url = "https://example.com/webhook",
            AuthenticationType = "Bearer",
            Token = "my-token"
        };
        var loggerMock = new Mock<ILogger>();

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("my-token", request.Headers.Authorization.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var sink = new WebhookEventSink(options, null, loggerMock.Object, httpClient);

        var cloudEvent = new CloudEvent { Id = "123", Type = "TestEvent", Source = new Uri("urn:test") };

        // Act
        await sink.SendEventsAsync(new[] { cloudEvent });
    }
}
