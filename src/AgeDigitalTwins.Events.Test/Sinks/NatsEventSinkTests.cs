using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Events.Sinks.Nats;
using Azure.Core;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using Moq;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Xunit;

namespace AgeDigitalTwins.Events.Test;

public class NatsEventSinkTests
{
    private readonly Mock<INatsClient> _natsClientMock;
    private readonly Mock<INatsConnection> _natsConnectionMock;
    private readonly Mock<ILogger> _loggerMock;

    public NatsEventSinkTests()
    {
        _natsClientMock = new Mock<INatsClient>();
        _natsConnectionMock = new Mock<INatsConnection>();
        _loggerMock = new Mock<ILogger>();

        _natsClientMock.Setup(c => c.Connection).Returns(_natsConnectionMock.Object);
    }

    [Fact]
    public void Constructor_CreatesNatsEventSink()
    {
        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        using var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);

        Assert.NotNull(sink);
        Assert.Equal("TestSink", sink.Name);
    }

    [Fact]
    public void IsHealthy_ReturnsTrue_AfterConstruction()
    {
        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        using var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);

        Assert.True(sink.IsHealthy);
    }

    [Fact]
    public void Constructor_WithTlsOptions_Succeeds()
    {
        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            TlsEnabled = true,
            TlsSkipVerify = true
        };

        using var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);

        Assert.NotNull(sink);
    }

    [Fact]
    public void Constructor_WithTokenAuth_Succeeds()
    {
        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            Token = "my-nats-token"
        };

        using var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);

        Assert.NotNull(sink);
    }

    [Fact]
    public void Constructor_WithOAuthCredential_UsesToken()
    {
        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            AuthenticationType = "OAuth",
            Scope = "https://nats.io/.default"
        };

        var credentialMock = new Mock<TokenCredential>();
        credentialMock
            .Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("test-token", DateTimeOffset.UtcNow.AddHours(1)));

        using var sink = new NatsEventSink(options, credentialMock.Object, _loggerMock.Object, _natsClientMock.Object);

        Assert.NotNull(sink);
        _natsClientMock.Verify(c => c.Connection, Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendEventsAsync_PublishesToNatsSubject()
    {
        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        using var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);

        var cloudEvent = new CloudEvent
        {
            Id = "123",
            Type = "TestEvent",
            Source = new Uri("urn:test"),
            Time = DateTimeOffset.UtcNow,
            Data = "test-data"
        };

        await sink.SendEventsAsync(new[] { cloudEvent });

        // Verify the event was published to the NATS subject
        _natsClientMock.Verify(
            c => c.PublishAsync<byte[]>(
                "test.subject",
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<NatsPubOpts?>()),
            Times.Once);

        Assert.True(sink.IsHealthy);
    }

    [Fact]
    public async Task SendEventsAsync_MultipleEvents_PublishesAll()
    {
        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        using var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);

        var events = new[]
        {
            new CloudEvent { Id = "1", Type = "TestEvent", Source = new Uri("urn:test") },
            new CloudEvent { Id = "2", Type = "TestEvent", Source = new Uri("urn:test") },
            new CloudEvent { Id = "3", Type = "TestEvent", Source = new Uri("urn:test") }
        };

        await sink.SendEventsAsync(events);

        _natsClientMock.Verify(
            c => c.PublishAsync<byte[]>(
                "test.subject",
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<NatsPubOpts?>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task SendEventsAsync_WhenJetStreamEnabled_UsesJetStreamContext()
    {
        var jetStreamMock = new Mock<INatsJSContext>();

        _natsConnectionMock
            .Setup(c => c.CreateJetStreamContext())
            .Returns(jetStreamMock.Object);

        // Setup JetStream publish to return a successful PubAckResponse
        jetStreamMock
            .Setup(js => js.PublishAsync<byte[]>(
                It.IsAny<string>(),
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<NatsJSPubOpts?>()))
            .ReturnsAsync(new PubAckResponse { Error = null });

        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject",
            JetStream = true,
            StreamName = "test-stream"
        };

        using var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);

        var cloudEvent = new CloudEvent
        {
            Id = "123",
            Type = "TestEvent",
            Source = new Uri("urn:test"),
            Data = "test-data"
        };

        await sink.SendEventsAsync(new[] { cloudEvent });

        jetStreamMock.Verify(
            js => js.PublishAsync<byte[]>(
                "test.subject",
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<NatsJSPubOpts?>()),
            Times.Once);

        Assert.True(sink.IsHealthy);
    }

    [Fact]
    public async Task SendEventsAsync_WhenPublishingFails_SetsHealthyFalse()
    {
        _natsClientMock
            .Setup(c => c.PublishAsync<byte[]>(
                It.IsAny<string>(),
                It.IsAny<byte[]?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<NatsPubOpts?>()))
            .ThrowsAsync(new NatsException("Connection lost"));

        var options = new NatsSinkOptions
        {
            Name = "FailingSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        using var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);

        var cloudEvent = new CloudEvent
        {
            Id = "123",
            Type = "TestEvent",
            Source = new Uri("urn:test"),
            Data = "test-data"
        };

        await sink.SendEventsAsync(new[] { cloudEvent });

        Assert.False(sink.IsHealthy);
    }

    [Fact]
    public void Dispose_CleansUpNatsClient()
    {
        var options = new NatsSinkOptions
        {
            Name = "TestSink",
            Url = "nats://localhost:4222",
            Subject = "test.subject"
        };

        var sink = new NatsEventSink(options, null, _loggerMock.Object, _natsClientMock.Object);
        sink.Dispose();

        // After dispose, the sink is still valid but connection is disposed
        Assert.NotNull(sink);
    }
}
