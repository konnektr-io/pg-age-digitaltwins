using System.Text.Json;
using AgeDigitalTwins.Test;
using Azure.DigitalTwins.Core;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Events.Test;

[Trait("Category", "Integration")]
public class EventsIntegrationTests : EventsTestBase
{
    private readonly ITestOutputHelper _output;

    public EventsIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateDigitalTwin_ShouldGenerateTwinCreateEvent()
    {
        // Arrange
        await WaitForReplicationHealthy();

        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        var uniqueTwinId = $"crater_{Guid.NewGuid():N}";
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        digitalTwin!.Id = uniqueTwinId;

        // Clear any existing events
        TestSink.ClearEvents();

        // Act
        await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin.Id, digitalTwin);

        // Assert - Wait for the specific event
        var receivedEvent = TestSink.WaitForEvent(
            uniqueTwinId,
            "Konnektr.DigitalTwins.Twin.Create",
            TimeSpan.FromSeconds(10)
        );

        Assert.NotNull(receivedEvent);
        Assert.Equal("Konnektr.DigitalTwins.Twin.Create", receivedEvent.Type);
        Assert.Equal(uniqueTwinId, receivedEvent.Subject);
        Assert.Equal("application/json", receivedEvent.DataContentType);

        _output.WriteLine($"Successfully captured twin create event for {uniqueTwinId}");
        _output.WriteLine($"Event ID: {receivedEvent.Id}");
        _output.WriteLine($"Event Type: {receivedEvent.Type}");
        _output.WriteLine($"Event Source: {receivedEvent.Source}");
    }

    [Fact]
    public async Task UpdateDigitalTwin_ShouldGenerateTwinUpdateEvent()
    {
        // Arrange
        await WaitForReplicationHealthy();

        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        var uniqueTwinId = $"crater_{Guid.NewGuid():N}";
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        digitalTwin!.Id = uniqueTwinId;

        // Create the twin first
        await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin.Id, digitalTwin);

        // Clear events after creation
        TestSink.ClearEvents();

        // Act - Update the twin
        digitalTwin.Contents["diameter"] = 250.0; // Update diameter property
        await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin.Id, digitalTwin);

        // Assert - Wait for the update event
        var receivedEvent = TestSink.WaitForEvent(
            uniqueTwinId,
            "Konnektr.DigitalTwins.Twin.Update",
            TimeSpan.FromSeconds(10)
        );

        Assert.NotNull(receivedEvent);
        Assert.Equal("Konnektr.DigitalTwins.Twin.Update", receivedEvent.Type);
        Assert.Equal(uniqueTwinId, receivedEvent.Subject);
        Assert.Equal("application/json", receivedEvent.DataContentType);

        _output.WriteLine($"Successfully captured twin update event for {uniqueTwinId}");
        _output.WriteLine($"Event ID: {receivedEvent.Id}");
        _output.WriteLine($"Event Type: {receivedEvent.Type}");
    }

    [Fact]
    public async Task DeleteDigitalTwin_ShouldGenerateTwinDeleteEvent()
    {
        // Arrange
        await WaitForReplicationHealthy();

        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        var uniqueTwinId = $"crater_{Guid.NewGuid():N}";
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        digitalTwin!.Id = uniqueTwinId;

        // Create the twin first
        await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin.Id, digitalTwin);

        // Clear events after creation
        TestSink.ClearEvents();

        // Act - Delete the twin
        await Client.DeleteDigitalTwinAsync(digitalTwin.Id);

        // Assert - Wait for the delete event
        var receivedEvent = TestSink.WaitForEvent(
            uniqueTwinId,
            "Konnektr.DigitalTwins.Twin.Delete",
            TimeSpan.FromSeconds(10)
        );

        Assert.NotNull(receivedEvent);
        Assert.Equal("Konnektr.DigitalTwins.Twin.Delete", receivedEvent.Type);
        Assert.Equal(uniqueTwinId, receivedEvent.Subject);
        Assert.Equal("application/json", receivedEvent.DataContentType);

        _output.WriteLine($"Successfully captured twin delete event for {uniqueTwinId}");
        _output.WriteLine($"Event ID: {receivedEvent.Id}");
        _output.WriteLine($"Event Type: {receivedEvent.Type}");
    }

    [Fact]
    public async Task CreateRelationship_ShouldGenerateRelationshipCreateEvent()
    {
        // Arrange
        await WaitForReplicationHealthy();

        string[] models = [SampleData.DtdlCrater, SampleData.DtdlRoom];
        await Client.CreateModelsAsync(models);

        var sourceTwinId = $"crater_{Guid.NewGuid():N}";
        var targetTwinId = $"room_{Guid.NewGuid():N}";
        var relationshipId = $"rel_{Guid.NewGuid():N}";

        // Create twins
        var sourceTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        sourceTwin!.Id = sourceTwinId;
        await Client.CreateOrReplaceDigitalTwinAsync(sourceTwin.Id, sourceTwin);

        var targetTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinRoom1);
        targetTwin!.Id = targetTwinId;
        await Client.CreateOrReplaceDigitalTwinAsync(targetTwin.Id, targetTwin);

        // Clear events after twin creation
        TestSink.ClearEvents();

        // Act - Create relationship
        var relationship = new BasicRelationship
        {
            Id = relationshipId,
            SourceId = sourceTwinId,
            TargetId = targetTwinId,
            Name =
                "contains" // Assuming crater can contain room for test purposes
            ,
        };

        await Client.CreateOrReplaceRelationshipAsync(sourceTwinId, relationshipId, relationship);

        // Assert - Wait for the relationship create event
        var expectedSubject = $"{sourceTwinId}/relationships/{relationshipId}";
        var receivedEvent = TestSink.WaitForEvent(
            expectedSubject,
            "Konnektr.DigitalTwins.Relationship.Create",
            TimeSpan.FromSeconds(10)
        );

        Assert.NotNull(receivedEvent);
        Assert.Equal("Konnektr.DigitalTwins.Relationship.Create", receivedEvent.Type);
        Assert.Equal(expectedSubject, receivedEvent.Subject);
        Assert.Equal("application/json", receivedEvent.DataContentType);

        _output.WriteLine($"Successfully captured relationship create event for {relationshipId}");
        _output.WriteLine($"Event ID: {receivedEvent.Id}");
        _output.WriteLine($"Event Type: {receivedEvent.Type}");
        _output.WriteLine($"Event Subject: {receivedEvent.Subject}");
    }

    [Fact]
    public async Task BatchOperations_ShouldGenerateMultipleEvents()
    {
        // Arrange
        await WaitForReplicationHealthy();

        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        var twin1Id = $"crater1_{Guid.NewGuid():N}";
        var twin2Id = $"crater2_{Guid.NewGuid():N}";

        var twin1 = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        twin1!.Id = twin1Id;

        var twin2 = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        twin2!.Id = twin2Id;

        // Clear any existing events
        TestSink.ClearEvents();

        // Act - Create both twins (this should generate separate events due to our entity detection)
        var tasks = new[]
        {
            Client.CreateOrReplaceDigitalTwinAsync(twin1.Id, twin1),
            Client.CreateOrReplaceDigitalTwinAsync(twin2.Id, twin2),
        };

        await Task.WhenAll(tasks);

        // Assert - Wait for both events
        await Task.Delay(2000); // Give some time for events to be processed

        var allEvents = TestSink.GetCapturedEvents().ToList();
        var twin1Events = allEvents.Where(e => e.Subject == twin1Id).ToList();
        var twin2Events = allEvents.Where(e => e.Subject == twin2Id).ToList();

        Assert.True(twin1Events.Count > 0, $"Should have at least one event for {twin1Id}");
        Assert.True(twin2Events.Count > 0, $"Should have at least one event for {twin2Id}");

        _output.WriteLine($"Total events captured: {allEvents.Count}");
        _output.WriteLine($"Events for {twin1Id}: {twin1Events.Count}");
        _output.WriteLine($"Events for {twin2Id}: {twin2Events.Count}");

        foreach (var evt in allEvents)
        {
            _output.WriteLine($"Event: {evt.Type} - Subject: {evt.Subject} - ID: {evt.Id}");
        }
    }
}
