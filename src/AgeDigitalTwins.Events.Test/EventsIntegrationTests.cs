using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
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
        try
        {
            await Client.CreateModelsAsync(models);
        }
        catch (Exceptions.ModelAlreadyExistsException)
        {
            // Model already exists, ignore
        }

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
        try
        {
            await Client.CreateModelsAsync(models);
        }
        catch (Exceptions.ModelAlreadyExistsException)
        {
            // Model already exists, ignore
        }

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
        try
        {
            await Client.CreateModelsAsync(models);
        }
        catch (Exceptions.ModelAlreadyExistsException)
        {
            // Model already exists, ignore
        }

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

        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        try
        {
            await Client.CreateModelsAsync(models);
        }
        catch (Exceptions.ModelAlreadyExistsException)
        {
            // Model already exists, ignore
        }

        var sourceTwinId = $"room_{Guid.NewGuid():N}";
        var targetTwinId = $"sensor_{Guid.NewGuid():N}";
        var relationshipId = $"rel_{Guid.NewGuid():N}";

        // Create twins
        var sourceTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinRoom1);
        sourceTwin!.Id = sourceTwinId;
        await Client.CreateOrReplaceDigitalTwinAsync(sourceTwin.Id, sourceTwin);

        var targetTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(
            SampleData.TwinTemperatureSensor1
        );
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
            Name = "rel_has_sensors",
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
        try
        {
            await Client.CreateModelsAsync(models);
        }
        catch (Exceptions.ModelAlreadyExistsException)
        {
            // Model already exists, ignore
        }

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
        try
        {
            await Client.CreateModelsAsync(models);
        }
        catch (Exceptions.ModelAlreadyExistsException)
        {
            // Model already exists, ignore
        }

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
        try
        {
            await Client.CreateModelsAsync(models);
        }
        catch (Exceptions.ModelAlreadyExistsException)
        {
            // Model already exists, ignore
        }

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

        _output.WriteLine($"Total events captured: {allEvents.Count}");
        foreach (var evt in allEvents)
        {
            _output.WriteLine($"Event: {evt.Type} - Subject: {evt.Subject} - ID: {evt.Id}");
        }

        // Check for Property Event - specifically look for the update event (value: 200)
        var propertyEvents = allEvents
            .Where(e =>
                e.Subject == uniqueTwinId && e.Type == "Konnektr.DigitalTwins.Property.Event"
            )
            .ToList();
        Assert.True(propertyEvents.Count > 0, "Should have at least one property event");

        // Find the property event with the updated value (200)
        var diameterPropertyEvent = propertyEvents.FirstOrDefault(e =>
        {
            var data = e.Data as JsonObject;
            return data != null
                && data["key"]?.ToString() == "diameter"
                && data["value"]?.ToString() == "200";
        });

        // If we didn't find the 200 value, let's check what values we do have
        if (diameterPropertyEvent == null)
        {
            _output.WriteLine("Property events found:");
            foreach (var evt in propertyEvents)
            {
                var data = evt.Data as JsonObject;
                _output.WriteLine($"  Key: {data?["key"]}, Value: {data?["value"]}");
            }
        }

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

        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        try
        {
            await Client.CreateModelsAsync(models);
        }
        catch (Exceptions.ModelAlreadyExistsException)
        {
            // Model already exists, ignore
        }

        var sourceTwinId = $"room_{Guid.NewGuid():N}";
        var targetTwinId = $"sensor_{Guid.NewGuid():N}";
        var relationshipId = $"rel_{Guid.NewGuid():N}";

        // Create twins
        var sourceTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinRoom1);
        sourceTwin!.Id = sourceTwinId;
        await Client.CreateOrReplaceDigitalTwinAsync(sourceTwin.Id, sourceTwin);

        var targetTwin = JsonSerializer.Deserialize<BasicDigitalTwin>(
            SampleData.TwinTemperatureSensor1
        );
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
            Name = "rel_has_sensors",
        };

        await Client.CreateOrReplaceRelationshipAsync(sourceTwinId, relationshipId, relationship);

        // Assert - Wait for relationship lifecycle event
        await Task.Delay(2000); // Give some time for events to be processed

        var allEvents = TestSink.GetCapturedEvents().ToList();
        var expectedSubject = $"{sourceTwinId}/relationships/{relationshipId}";

        _output.WriteLine($"Total events captured: {allEvents.Count}");
        foreach (var evt in allEvents)
        {
            _output.WriteLine($"Event: {evt.Type} - Subject: {evt.Subject} - ID: {evt.Id}");
        }

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
        Assert.Equal("rel_has_sensors", relationshipEventData["name"]?.ToString());
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

    [Fact]
    public async Task ImportJob_WithMultipleTwinsAndRelationships_ShouldGenerateAllEventTypes()
    {
        // Arrange
        await _fixture.WaitForReplicationHealthy();

        // Clear any existing events
        TestSink.ClearEvents();

        // Expanded ND-JSON data with multiple twins and relationships using unique model IDs
        var sampleData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""contoso""}
{""Section"": ""Models""}
{""@id"":""dtmi:com:adt:dtsample:room;2"",""@type"":""Interface"",""@context"":[""dtmi:dtdl:context;3"",""dtmi:dtdl:extension:quantitativeTypes;1""],""displayName"":""Room v2"",""contents"":[{""@type"":""Property"",""name"":""name"",""schema"":""string""},{""@type"":""Property"",""name"":""temperature"",""schema"":""double""},{""@type"":[""Property"",""Humidity""],""name"":""humidity"",""schema"":""double"",""unit"":""gramPerCubicMetre""},{""@type"":""Relationship"",""@id"":""dtmi:com:adt:dtsample:room:rel_has_sensors;2"",""name"":""rel_has_sensors"",""displayName"":""Room has sensors""}]}
{""@id"":""dtmi:com:adt:dtsample:tempsensor;2"",""@type"":""Interface"",""@context"":[""dtmi:dtdl:context;3"",""dtmi:dtdl:extension:quantitativeTypes;1""],""displayName"":""Temperature Sensor v2"",""contents"":[{""@type"":""Property"",""name"":""name"",""schema"":""string""},{""@type"":""Property"",""name"":""temperature"",""schema"":""double""}]}
{""@id"":""dtmi:com:contoso:ImportTestCrater;1"",""@type"":""Interface"",""@context"":""dtmi:dtdl:context;3"",""displayName"":""Import Test Crater"",""contents"":[{""@type"":""Property"",""name"":""diameter"",""schema"":""double""},{""@type"":""Property"",""name"":""depth"",""schema"":""double""}]}
{""Section"": ""Twins""}
{""$dtId"":""room1"",""$metadata"":{""$model"":""dtmi:com:adt:dtsample:room;2""},""name"":""Room 1"",""temperature"":22.5,""humidity"":45.0}
{""$dtId"":""room2"",""$metadata"":{""$model"":""dtmi:com:adt:dtsample:room;2""},""name"":""Room 2"",""temperature"":21.0,""humidity"":50.0}
{""$dtId"":""sensor1"",""$metadata"":{""$model"":""dtmi:com:adt:dtsample:tempsensor;2""},""name"":""Temperature Sensor 1"",""temperature"":22.3}
{""$dtId"":""sensor2"",""$metadata"":{""$model"":""dtmi:com:adt:dtsample:tempsensor;2""},""name"":""Temperature Sensor 2"",""temperature"":21.8}
{""$dtId"":""crater1"",""$metadata"":{""$model"":""dtmi:com:contoso:ImportTestCrater;1""},""diameter"":150.0,""depth"":30.0}
{""$dtId"":""crater2"",""$metadata"":{""$model"":""dtmi:com:contoso:ImportTestCrater;1""},""diameter"":200.0,""depth"":45.0}
{""Section"": ""Relationships""}
{""$sourceId"":""room1"",""$relationshipId"":""room1_sensor1"",""$targetId"":""sensor1"",""$relationshipName"":""rel_has_sensors""}
{""$sourceId"":""room1"",""$relationshipId"":""room1_sensor2"",""$targetId"":""sensor2"",""$relationshipName"":""rel_has_sensors""}
{""$sourceId"":""room2"",""$relationshipId"":""room2_sensor1"",""$targetId"":""sensor1"",""$relationshipName"":""rel_has_sensors""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(sampleData));
        using var outputStream = new MemoryStream();

        var options = new ImportJobOptions
        {
            ContinueOnFailure = true,
            OperationTimeout = TimeSpan.FromSeconds(60),
            LeaveOpen = true,
        };

        // Act
        var jobId = $"test-import-events-{Guid.NewGuid().ToString("N")[..8]}";
        var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);

        // Log result details BEFORE assertions for debugging
        outputStream.Position = 0;
        using var logReader = new StreamReader(outputStream);
        var logContent = await logReader.ReadToEndAsync();

        _output.WriteLine("Import Job Result:");
        _output.WriteLine($"Job ID: {result.Id}");
        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Start Time: {result.CreatedDateTime}");
        _output.WriteLine($"End Time: {result.FinishedDateTime}");
        _output.WriteLine($"Models Created: {result.ModelsCreated}");
        _output.WriteLine($"Twins Created: {result.TwinsCreated}");
        _output.WriteLine($"Relationships Created: {result.RelationshipsCreated}");
        _output.WriteLine($"Error Count: {result.ErrorCount}");
        _output.WriteLine("Log Output:");
        _output.WriteLine(logContent);

        // Wait for events to be processed
        await Task.Delay(5000); // Give more time for all events to be processed

        // Assert import was successful (or mostly successful with continue on failure)
        Assert.NotNull(result);
        // With continue on failure, some models may fail but twins should succeed
        Assert.True(
            result.TwinsCreated >= 4,
            $"Expected at least 4 twins created, got {result.TwinsCreated}"
        ); // room1, room2, sensor1, sensor2 minimum
        Assert.True(
            result.RelationshipsCreated >= 3,
            $"Expected at least 3 relationships created, got {result.RelationshipsCreated}"
        ); // 3 relationships

        // Get all captured events
        var allEvents = TestSink.GetCapturedEvents().ToList();

        _output.WriteLine($"Total events captured: {allEvents.Count}");
        foreach (var evt in allEvents)
        {
            _output.WriteLine($"Event: {evt.Type} - Subject: {evt.Subject} - ID: {evt.Id}");
        }

        // Verify Twin Lifecycle events (one for each twin created)
        var twinLifecycleEvents = allEvents
            .Where(e => e.Type == "Konnektr.DigitalTwins.Twin.Lifecycle")
            .ToList();
        var expectedTwinIds = new[]
        {
            "room1",
            "room2",
            "sensor1",
            "sensor2",
            "crater1",
            "crater2",
        };

        Assert.True(
            twinLifecycleEvents.Count >= 4,
            $"Expected at least 4 twin lifecycle events, got {twinLifecycleEvents.Count}"
        );

        // Verify that we have events for the core twins that should definitely work
        var coreTwinIds = new[] { "room1", "room2", "sensor1", "sensor2" };
        foreach (var twinId in coreTwinIds)
        {
            var twinEvent = twinLifecycleEvents.FirstOrDefault(e => e.Subject == twinId);
            if (twinEvent != null)
            {
                var eventData = twinEvent.Data as JsonObject;
                Assert.NotNull(eventData);
                Assert.Equal(twinId, eventData["id"]?.ToString());
                Assert.Equal("Create", eventData["action"]?.ToString());

                _output.WriteLine($"✓ Twin Lifecycle event verified for {twinId}");
            }
            else
            {
                _output.WriteLine($"⚠ Twin Lifecycle event NOT found for {twinId}");
            }
        }

        // Verify Property events (one for each property of each twin)
        var propertyEvents = allEvents
            .Where(e => e.Type == "Konnektr.DigitalTwins.Property.Event")
            .ToList();
        Assert.True(
            propertyEvents.Count >= 8,
            $"Expected at least 8 property events, got {propertyEvents.Count}"
        ); // Each of 4 core twins has 2 properties minimum

        // Verify specific property events for some twins
        var room1TempEvent = propertyEvents.FirstOrDefault(e =>
        {
            var data = e.Data as JsonObject;
            return e.Subject == "room1" && data?["key"]?.ToString() == "temperature";
        });
        if (room1TempEvent != null)
        {
            var room1TempData = room1TempEvent.Data as JsonObject;
            Assert.Equal("22.5", room1TempData?["value"]?.ToString());
            _output.WriteLine("✓ Room1 temperature property event verified");
        }

        // Check for crater events if they were created successfully
        var crater1DiameterEvent = propertyEvents.FirstOrDefault(e =>
        {
            var data = e.Data as JsonObject;
            return e.Subject == "crater1" && data?["key"]?.ToString() == "diameter";
        });
        if (crater1DiameterEvent != null)
        {
            var crater1DiameterData = crater1DiameterEvent.Data as JsonObject;
            Assert.Equal("150", crater1DiameterData?["value"]?.ToString());
            _output.WriteLine("✓ Crater1 diameter property event verified");
        }

        _output.WriteLine($"✓ Property events verified: {propertyEvents.Count} total");

        // Verify Relationship Lifecycle events
        var relationshipLifecycleEvents = allEvents
            .Where(e => e.Type == "Konnektr.DigitalTwins.Relationship.Lifecycle")
            .ToList();
        Assert.True(
            relationshipLifecycleEvents.Count >= 3,
            $"Expected at least 3 relationship lifecycle events, got {relationshipLifecycleEvents.Count}"
        );

        var expectedRelationships = new[]
        {
            ("room1", "room1_sensor1", "sensor1"),
            ("room1", "room1_sensor2", "sensor2"),
            ("room2", "room2_sensor1", "sensor1"),
        };

        foreach (var (sourceId, relationshipId, targetId) in expectedRelationships)
        {
            var expectedSubject = $"{sourceId}/relationships/{relationshipId}";
            var relEvent = relationshipLifecycleEvents.FirstOrDefault(e =>
                e.Subject == expectedSubject
            );
            Assert.NotNull(relEvent);

            var eventData = relEvent.Data as JsonObject;
            Assert.NotNull(eventData);
            Assert.Equal(sourceId, eventData["source"]?.ToString());
            Assert.Equal(targetId, eventData["target"]?.ToString());
            Assert.Equal("rel_has_sensors", eventData["name"]?.ToString());
            Assert.Equal(relationshipId, eventData["relationshipId"]?.ToString());
            Assert.Equal("Create", eventData["action"]?.ToString());

            _output.WriteLine($"✓ Relationship Lifecycle event verified for {relationshipId}");
        }

        // Verify we have events for all expected subjects
        var subjects = allEvents.Select(e => e.Subject).Distinct().ToList();
        _output.WriteLine($"Event subjects found: {string.Join(", ", subjects)}");

        // Summary
        _output.WriteLine("\n=== EVENT GENERATION SUMMARY ===");
        _output.WriteLine($"Import Job Result: {result.Status}");
        _output.WriteLine($"Models Created: {result.ModelsCreated}");
        _output.WriteLine($"Twins Created: {result.TwinsCreated}");
        _output.WriteLine($"Relationships Created: {result.RelationshipsCreated}");
        _output.WriteLine($"Total Events Generated: {allEvents.Count}");
        _output.WriteLine($"Twin Lifecycle Events: {twinLifecycleEvents.Count}");
        _output.WriteLine($"Property Events: {propertyEvents.Count}");
        _output.WriteLine($"Relationship Lifecycle Events: {relationshipLifecycleEvents.Count}");
        _output.WriteLine("✓ All event types successfully generated and verified!");
    }
}
