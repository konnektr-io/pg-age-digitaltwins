using System.Text.Json;
using System.Text.Json.Nodes;
using AgeDigitalTwins.Test;
using Azure.DigitalTwins.Core;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Events.Test;

// Define a collection to ensure Events tests run sequentially (not in parallel)
[CollectionDefinition("EventsIntegration", DisableParallelization = true)]
public class EventsIntegrationCollection;

[Collection("EventsIntegration")]
[Trait("Category", "Integration")]
public class EventsIntegrationTests : IClassFixture<EventsFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly EventsFixture _fixture;

    // Convenience properties to access fixture members
    protected AgeDigitalTwinsClient Client => _fixture.Client;
    protected TestingEventSink TestSink => _fixture.TestSink;

    public EventsIntegrationTests(EventsFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task CreateDigitalTwin_ShouldGenerateTwinCreateEvent()
    {
        // Arrange
        await _fixture.WaitForReplicationHealthy();

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
        var receivedEvent = await TestSink.WaitForEventAsync(
            uniqueTwinId,
            "Konnektr.DigitalTwins.Twin.Create",
            TimeSpan.FromSeconds(30)
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
        await _fixture.WaitForReplicationHealthy();

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
        var receivedEvent = await TestSink.WaitForEventAsync(
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
        await _fixture.WaitForReplicationHealthy();

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
        var receivedEvent = await TestSink.WaitForEventAsync(
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
        await _fixture.WaitForReplicationHealthy();

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
        var receivedEvent = await TestSink.WaitForEventAsync(
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
        await _fixture.WaitForReplicationHealthy();

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
        await Client.CreateOrReplaceDigitalTwinsAsync([twin1, twin2]);

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

    [Fact]
    public async Task CreateDigitalTwinWithProperties_ShouldGenerateDataHistoryEvents()
    {
        // Arrange
        await _fixture.WaitForReplicationHealthy();

        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        var uniqueTwinId = $"crater_{Guid.NewGuid():N}";
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        digitalTwin!.Id = uniqueTwinId;
        digitalTwin.Contents["diameter"] = 150.0; // Add a property value

        // Clear any existing events
        TestSink.ClearEvents();

        // Act
        await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin.Id, digitalTwin);

        // Assert - Wait for data history events
        await Task.Delay(2000); // Give some time for events to be processed

        var allEvents = TestSink.GetCapturedEvents().ToList();

        // Check for Twin Lifecycle event
        var lifecycleEvent = allEvents.FirstOrDefault(e =>
            e.Subject == uniqueTwinId && e.Type == "Konnektr.DigitalTwins.Twin.Lifecycle"
        );
        Assert.NotNull(lifecycleEvent);
        Assert.Equal("application/json", lifecycleEvent.DataContentType);

        // Check for Property Event (for the diameter property)
        var propertyEvent = allEvents.FirstOrDefault(e =>
            e.Subject == uniqueTwinId && e.Type == "Konnektr.DigitalTwins.Property.Event"
        );
        Assert.NotNull(propertyEvent);
        Assert.Equal("application/json", propertyEvent.DataContentType);

        // Verify property event data structure
        var propertyEventData = propertyEvent.Data as JsonObject;
        Assert.NotNull(propertyEventData);
        Assert.Equal(uniqueTwinId, propertyEventData["id"]?.ToString());
        Assert.Equal("dtmi:com:contoso:Crater;1", propertyEventData["modelId"]?.ToString());
        Assert.Equal("diameter", propertyEventData["key"]?.ToString());
        Assert.Equal("150", propertyEventData["value"]?.ToString());

        _output.WriteLine($"Successfully captured data history events for {uniqueTwinId}");
        _output.WriteLine($"Lifecycle Event: {lifecycleEvent.Type} - ID: {lifecycleEvent.Id}");
        _output.WriteLine($"Property Event: {propertyEvent.Type} - ID: {propertyEvent.Id}");
        _output.WriteLine($"Total events captured: {allEvents.Count}");

        foreach (var evt in allEvents)
        {
            _output.WriteLine($"Event: {evt.Type} - Subject: {evt.Subject} - ID: {evt.Id}");
        }
    }

    [Fact]
    public async Task UpdateDigitalTwinProperty_ShouldGeneratePropertyEvent()
    {
        // Arrange
        await _fixture.WaitForReplicationHealthy();

        string[] models = [SampleData.DtdlCrater];
        await Client.CreateModelsAsync(models);

        var uniqueTwinId = $"crater_{Guid.NewGuid():N}";
        var digitalTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater);
        digitalTwin!.Id = uniqueTwinId;
        digitalTwin.Contents["diameter"] = 100.0;

        // Create the twin first
        await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin.Id, digitalTwin);

        // Clear events after creation
        TestSink.ClearEvents();

        // Act - Update the property
        digitalTwin.Contents["diameter"] = 200.0;
        await Client.CreateOrReplaceDigitalTwinAsync(digitalTwin.Id, digitalTwin);

        // Assert - Wait for property event
        await Task.Delay(2000); // Give some time for events to be processed

        var allEvents = TestSink.GetCapturedEvents().ToList();

        // Check for Property Event
        var propertyEvents = allEvents
            .Where(e =>
                e.Subject == uniqueTwinId && e.Type == "Konnektr.DigitalTwins.Property.Event"
            )
            .ToList();
        Assert.True(propertyEvents.Count > 0, "Should have at least one property event");

        var diameterPropertyEvent = propertyEvents.FirstOrDefault(e =>
        {
            var data = e.Data as JsonObject;
            return data != null && data["key"]?.ToString() == "diameter";
        });
        Assert.NotNull(diameterPropertyEvent);

        // Verify property event data structure
        var propertyEventData = diameterPropertyEvent.Data as JsonObject;
        Assert.NotNull(propertyEventData);
        Assert.Equal(uniqueTwinId, propertyEventData["id"]?.ToString());
        Assert.Equal("dtmi:com:contoso:Crater;1", propertyEventData["modelId"]?.ToString());
        Assert.Equal("diameter", propertyEventData["key"]?.ToString());
        Assert.Equal("200", propertyEventData["value"]?.ToString());

        _output.WriteLine($"Successfully captured property update event for {uniqueTwinId}");
        _output.WriteLine(
            $"Property Event: {diameterPropertyEvent.Type} - ID: {diameterPropertyEvent.Id}"
        );
        _output.WriteLine($"Property Key: {propertyEventData["key"]}");
        _output.WriteLine($"Property Value: {propertyEventData["value"]}");
    }

    [Fact]
    public async Task CreateRelationship_ShouldGenerateRelationshipLifecycleEvent()
    {
        // Arrange
        await _fixture.WaitForReplicationHealthy();

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
            Name = "contains",
        };

        await Client.CreateOrReplaceRelationshipAsync(sourceTwinId, relationshipId, relationship);

        // Assert - Wait for relationship lifecycle event
        await Task.Delay(2000); // Give some time for events to be processed

        var allEvents = TestSink.GetCapturedEvents().ToList();
        var expectedSubject = $"{sourceTwinId}/relationships/{relationshipId}";

        // Check for Relationship Lifecycle event
        var relationshipLifecycleEvent = allEvents.FirstOrDefault(e =>
            e.Subject == expectedSubject && e.Type == "Konnektr.DigitalTwins.Relationship.Lifecycle"
        );
        Assert.NotNull(relationshipLifecycleEvent);
        Assert.Equal("application/json", relationshipLifecycleEvent.DataContentType);

        // Verify relationship lifecycle event data structure
        var relationshipEventData = relationshipLifecycleEvent.Data as JsonObject;
        Assert.NotNull(relationshipEventData);
        Assert.Equal(sourceTwinId, relationshipEventData["source"]?.ToString());
        Assert.Equal(targetTwinId, relationshipEventData["target"]?.ToString());
        Assert.Equal("contains", relationshipEventData["name"]?.ToString());
        Assert.Equal(relationshipId, relationshipEventData["relationshipId"]?.ToString());
        Assert.Equal("Create", relationshipEventData["action"]?.ToString());

        _output.WriteLine(
            $"Successfully captured relationship lifecycle event for {relationshipId}"
        );
        _output.WriteLine(
            $"Lifecycle Event: {relationshipLifecycleEvent.Type} - ID: {relationshipLifecycleEvent.Id}"
        );
        _output.WriteLine($"Relationship Action: {relationshipEventData["action"]}");
        _output.WriteLine(
            $"Source: {relationshipEventData["source"]} -> Target: {relationshipEventData["target"]}"
        );
    }
}
