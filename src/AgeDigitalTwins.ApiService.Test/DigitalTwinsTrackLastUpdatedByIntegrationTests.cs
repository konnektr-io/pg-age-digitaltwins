using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;

namespace AgeDigitalTwins.ApiService.Test;

public class TrackLastUpdatedByAppHostForHttp : DistributedApplicationFactory
{
    public TrackLastUpdatedByAppHostForHttp()
        : base(
            typeof(Projects.AgeDigitalTwins_AppHost),
            ["temp_graph_" + Guid.NewGuid().ToString("N")]
        ) { }

    protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
    {
        // Belt-and-suspenders: also set env vars and config in OnBuilderCreated
        // as fallback for parallel test class env var contention
        Environment.SetEnvironmentVariable("Parameters__TrackLastUpdatedBy", "true");
        Environment.SetEnvironmentVariable("Parameters__UserIdHeaderName", "X-User-Id");
        Environment.SetEnvironmentVariable("Parameters__ReturnTwinLevelLastUpdatedBy", "true");
        Environment.SetEnvironmentVariable("Parameters__ReturnRelationshipLevelLastUpdatedBy", "true");
        applicationBuilder.Configuration["Parameters:TrackLastUpdatedBy"] = "true";
        applicationBuilder.Configuration["Parameters:UserIdHeaderName"] = "X-User-Id";
        applicationBuilder.Configuration["Parameters:ReturnTwinLevelLastUpdatedBy"] = "true";
        applicationBuilder.Configuration["Parameters:ReturnRelationshipLevelLastUpdatedBy"] = "true";

        applicationBuilder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });
    }
}

[CollectionDefinition("TrackLastUpdatedBy", DisableParallelization = true)]
public class TrackLastUpdatedByTestCollection
{
}

[Collection("TrackLastUpdatedBy")]
[Trait("Category", "Integration")]
public class DigitalTwinsTrackLastUpdatedByIntegrationTests : IAsyncLifetime
{
    private const string TestUserId = "integration-test-user";

    private TrackLastUpdatedByAppHostForHttp? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        // Set env vars BEFORE creating the AppHost so Program.cs reads them as defaults
        Environment.SetEnvironmentVariable("Parameters__TrackLastUpdatedBy", "true");
        Environment.SetEnvironmentVariable("Parameters__UserIdHeaderName", "X-User-Id");
        Environment.SetEnvironmentVariable("Parameters__ReturnTwinLevelLastUpdatedBy", "true");
        Environment.SetEnvironmentVariable("Parameters__ReturnRelationshipLevelLastUpdatedBy", "true");
        _app = new TrackLastUpdatedByAppHostForHttp();
        await _app.StartAsync();
        _httpClient = _app.CreateHttpClient("apiservice");

        string[] models = [SampleData.DtdlTemperatureSensor];
        var modelPayload = models.Select(m => JsonDocument.Parse(m).RootElement).ToList();
        var modelResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(modelPayload), Encoding.UTF8, "application/json")
        );
        modelResponse.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        if (_httpClient != null)
        {
            await _httpClient.DeleteAsync("/graph/delete");
        }
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
        _httpClient?.Dispose();
    }

    private HttpRequestMessage CreatePutRequest(string twinId, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/digitaltwins/{twinId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-User-Id", TestUserId);
        return request;
    }

    private HttpRequestMessage CreateGetRequest(string twinId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/digitaltwins/{twinId}");
        request.Headers.Add("X-User-Id", TestUserId);
        return request;
    }

    private HttpRequestMessage CreatePatchRequest(string twinId, string patchBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/digitaltwins/{twinId}")
        {
            Content = new StringContent(patchBody, Encoding.UTF8, "application/json-patch+json"),
        };
        request.Headers.Add("X-User-Id", TestUserId);
        return request;
    }

    [Fact]
    public async Task CreateTwin_WithXUserIdHeader_ResponseHasLastUpdatedBy()
    {
        var twinJson = $$"""
            {
                "$dtId": "tlby-create-test",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 22
            }
            """;

        var response = await _httpClient!.SendAsync(CreatePutRequest("tlby-create-test", twinJson));
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = JsonDocument.Parse(content).RootElement;

        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata), "$metadata should exist");

        Assert.True(
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedByTwin),
            "$lastUpdatedBy should be inside $metadata"
        );
        Assert.Equal(TestUserId, lastUpdatedByTwin.GetString());

        Assert.True(
            metadata.TryGetProperty("$lastUpdateTime", out JsonElement lastUpdateTime),
            "$lastUpdateTime should be inside $metadata"
        );
        Assert.True(DateTimeOffset.TryParse(lastUpdateTime.GetString(), out _));

        Assert.False(
            root.TryGetProperty("$lastUpdatedBy", out _),
            "$lastUpdatedBy should not be at root level"
        );
    }

    [Fact]
    public async Task GetTwin_AfterCreateWithUserId_LastUpdatedByInMetadata()
    {
        var twinJson = $$"""
            {
                "$dtId": "tlby-get-test",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 25
            }
            """;

        var putResponse = await _httpClient!.SendAsync(CreatePutRequest("tlby-get-test", twinJson));
        putResponse.EnsureSuccessStatusCode();

        var getResponse = await _httpClient!.SendAsync(CreateGetRequest("tlby-get-test"));
        var content = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var root = JsonDocument.Parse(content).RootElement;

        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata), "$metadata should exist");

        Assert.True(
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedByTwin2),
            "$lastUpdatedBy should be inside $metadata"
        );
        Assert.Equal(TestUserId, lastUpdatedByTwin2.GetString());

        Assert.True(
            metadata.TryGetProperty("$lastUpdateTime", out _),
            "$lastUpdateTime should be inside $metadata"
        );

        Assert.False(
            root.TryGetProperty("$lastUpdatedBy", out _),
            "$lastUpdatedBy should not be at root level"
        );
    }

    [Fact]
    public async Task GetTwin_PerPropertyLastUpdatedByExists()
    {
        var twinJson = $$"""
            {
                "$dtId": "tlby-perprop-test",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 30
            }
            """;

        var putResponse = await _httpClient!.SendAsync(CreatePutRequest("tlby-perprop-test", twinJson));
        putResponse.EnsureSuccessStatusCode();

        var getResponse = await _httpClient!.SendAsync(CreateGetRequest("tlby-perprop-test"));
        var content = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var root = JsonDocument.Parse(content).RootElement;
        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata));

        Assert.True(
            metadata.TryGetProperty("temperature", out JsonElement tempMetadata),
            "per-property metadata for temperature should exist"
        );
        Assert.True(
            tempMetadata.TryGetProperty("lastUpdatedBy", out JsonElement tempLastUpdatedBy),
            "temperature.lastUpdatedBy should exist"
        );
        Assert.Equal(TestUserId, tempLastUpdatedBy.GetString());

        Assert.True(
            tempMetadata.TryGetProperty("lastUpdateTime", out _),
            "temperature.lastUpdateTime should exist"
        );
    }

    [Fact]
    public async Task PatchTwin_LastUpdatedByStillPresent()
    {
        var twinJson = $$"""
            {
                "$dtId": "tlby-patch-test",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 20
            }
            """;

        var putResponse = await _httpClient!.SendAsync(CreatePutRequest("tlby-patch-test", twinJson));
        putResponse.EnsureSuccessStatusCode();

        var patchBody = """[{ "op": "replace", "path": "/temperature", "value": 26 }]""";
        var patchResponse = await _httpClient!.SendAsync(CreatePatchRequest("tlby-patch-test", patchBody));
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        var getResponse = await _httpClient!.SendAsync(CreateGetRequest("tlby-patch-test"));
        var content = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var root = JsonDocument.Parse(content).RootElement;
        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata));

        Assert.True(
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedBy),
            "$lastUpdatedBy should be inside $metadata after patch"
        );
        Assert.Equal(TestUserId, lastUpdatedBy.GetString());

        Assert.True(root.TryGetProperty("temperature", out JsonElement tempProp));
        Assert.Equal(26, tempProp.GetInt32());
    }

    [Fact]
    public async Task QueryTwin_LastUpdatedByInMetadata()
    {
        var twinJson = $$"""
            {
                "$dtId": "tlby-query-test",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 35
            }
            """;

        var putResponse = await _httpClient!.SendAsync(CreatePutRequest("tlby-query-test", twinJson));
        putResponse.EnsureSuccessStatusCode();

        await Task.Delay(2000);

        var queryBody = new JsonObject { ["query"] = "SELECT * FROM digitaltwins" };
        var queryRequest = new HttpRequestMessage(HttpMethod.Post, "/query")
        {
            Content = new StringContent(queryBody.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        queryRequest.Headers.Add("X-User-Id", TestUserId);

        var queryResponse = await _httpClient!.SendAsync(queryRequest);
        var queryContent = await queryResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);

        var queryResult = JsonDocument.Parse(queryContent);
        var results = queryResult.RootElement.GetProperty("value").EnumerateArray().ToList();

        var found = false;
        JsonElement metadata = default;
        foreach (var result in results)
        {
            if (result.TryGetProperty("$dtId", out var id) && id.GetString() == "tlby-query-test")
            {
                found = result.TryGetProperty("$metadata", out metadata);
                break;
            }
        }
        Assert.True(found, "tlby-query-test should be in query results");
        Assert.True(
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedBy),
            "$lastUpdatedBy should be inside $metadata in query results"
        );
        Assert.Equal(TestUserId, lastUpdatedBy.GetString());
    }
}

public class TrackLastUpdatedByNoReturnAppHost : DistributedApplicationFactory
{
    public TrackLastUpdatedByNoReturnAppHost()
        : base(
            typeof(Projects.AgeDigitalTwins_AppHost),
            ["temp_graph_" + Guid.NewGuid().ToString("N")]
        ) { }

    protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
    {
        // Belt-and-suspenders: also set env vars and config in OnBuilderCreated
        // as fallback for parallel test class env var contention
        Environment.SetEnvironmentVariable("Parameters__TrackLastUpdatedBy", "true");
        Environment.SetEnvironmentVariable("Parameters__UserIdHeaderName", "X-User-Id");
        Environment.SetEnvironmentVariable("Parameters__ReturnTwinLevelLastUpdatedBy", "false");
        Environment.SetEnvironmentVariable("Parameters__ReturnRelationshipLevelLastUpdatedBy", "true");
        applicationBuilder.Configuration["Parameters:TrackLastUpdatedBy"] = "true";
        applicationBuilder.Configuration["Parameters:UserIdHeaderName"] = "X-User-Id";
        applicationBuilder.Configuration["Parameters:ReturnTwinLevelLastUpdatedBy"] = "false";
        applicationBuilder.Configuration["Parameters:ReturnRelationshipLevelLastUpdatedBy"] = "true";

        applicationBuilder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });
    }
}

[Collection("TrackLastUpdatedBy")]
[Trait("Category", "Integration")]
public class DigitalTwinsTrackLastUpdatedByStrippedIntegrationTests : IAsyncLifetime
{
    private const string TestUserId = "stripped-test-user";

    private TrackLastUpdatedByNoReturnAppHost? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        // Set env vars BEFORE creating the AppHost so Program.cs reads them as defaults
        Environment.SetEnvironmentVariable("Parameters__TrackLastUpdatedBy", "true");
        Environment.SetEnvironmentVariable("Parameters__UserIdHeaderName", "X-User-Id");
        Environment.SetEnvironmentVariable("Parameters__ReturnTwinLevelLastUpdatedBy", "false");
        Environment.SetEnvironmentVariable("Parameters__ReturnRelationshipLevelLastUpdatedBy", "true");
        _app = new TrackLastUpdatedByNoReturnAppHost();
        await _app.StartAsync();
        _httpClient = _app.CreateHttpClient("apiservice");

        string[] models = [SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor];
        var modelPayload = models.Select(m => JsonDocument.Parse(m).RootElement).ToList();
        var modelResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(modelPayload), Encoding.UTF8, "application/json")
        );
        modelResponse.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        if (_httpClient != null)
        {
            await _httpClient.DeleteAsync("/graph/delete");
        }
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
        _httpClient?.Dispose();
    }

    private HttpRequestMessage CreatePutRequest(string twinId, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/digitaltwins/{twinId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-User-Id", TestUserId);
        return request;
    }

    [Fact]
    public async Task GetTwin_TwinLevelLastUpdatedByStripped()
    {
        var twinJson = $$"""
            {
                "$dtId": "tlby-strip-get",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 22
            }
            """;

        var putResponse = await _httpClient!.SendAsync(CreatePutRequest("tlby-strip-get", twinJson));
        putResponse.EnsureSuccessStatusCode();

        var getResponse = await _httpClient!.GetAsync("/digitaltwins/tlby-strip-get");
        var content = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var root = JsonDocument.Parse(content).RootElement;
        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata));

        Assert.False(
            metadata.TryGetProperty("$lastUpdatedBy", out _),
            "$lastUpdatedBy should be stripped from $metadata when ReturnTwinLevelLastUpdatedBy=false"
        );

        Assert.True(
            metadata.TryGetProperty("$lastUpdateTime", out _),
            "$lastUpdateTime should remain in $metadata"
        );

        Assert.True(
            metadata.TryGetProperty("temperature", out JsonElement tempMetadata),
            "per-property metadata should remain"
        );
        Assert.True(
            tempMetadata.TryGetProperty("lastUpdatedBy", out _),
            "per-property lastUpdatedBy should remain"
        );
        Assert.True(
            tempMetadata.TryGetProperty("lastUpdateTime", out _),
            "per-property lastUpdateTime should remain"
        );
    }

    [Fact]
    public async Task Query_MultiColumnMatch_TwinLevelLastUpdatedByStripped()
    {
        // Create Room twin
        var roomJson = $$"""
            {
                "$dtId": "tlby-multi-room",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:room;1" },
                "name": "Test Room",
                "temperature": 22.5
            }
            """;
        var roomPut = await _httpClient!.SendAsync(CreatePutRequest("tlby-multi-room", roomJson));
        roomPut.EnsureSuccessStatusCode();

        // Create Sensor twin
        var sensorJson = $$"""
            {
                "$dtId": "tlby-multi-sensor",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 30
            }
            """;
        var sensorPut = await _httpClient!.SendAsync(CreatePutRequest("tlby-multi-sensor", sensorJson));
        sensorPut.EnsureSuccessStatusCode();

        // Create relationship
        var relBody = new JsonObject
        {
            ["$targetId"] = "tlby-multi-sensor",
            ["$relationshipName"] = "rel_has_sensors",
        };
        var relPut = await _httpClient.PutAsync(
            "/digitaltwins/tlby-multi-room/relationships/rel1",
            new StringContent(relBody.ToJsonString(), Encoding.UTF8, "application/json")
        );
        relPut.EnsureSuccessStatusCode();

        await Task.Delay(2000);

        // Execute MATCH query returning aliased columns
        var queryBody = new JsonObject
        {
            ["query"] = "SELECT T, S FROM DIGITALTWINS MATCH (T)-[:rel_has_sensors]->(S) WHERE T.$dtId = 'tlby-multi-room'",
        };
        var queryRequest = new HttpRequestMessage(HttpMethod.Post, "/query")
        {
            Content = new StringContent(queryBody.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        queryRequest.Headers.Add("X-User-Id", TestUserId);
        var queryResponse = await _httpClient.SendAsync(queryRequest);
        var queryContent = await queryResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);

        var queryResult = JsonDocument.Parse(queryContent);
        var results = queryResult.RootElement.GetProperty("value").EnumerateArray().ToList();
        Assert.NotEmpty(results);

        var row = results[0];
        Assert.True(row.TryGetProperty("T", out JsonElement tTwin), "Query should return T alias");
        Assert.True(row.TryGetProperty("S", out JsonElement sTwin), "Query should return S alias");

        // Verify $lastUpdatedBy is stripped from both nested twins
        Assert.True(tTwin.TryGetProperty("$metadata", out JsonElement tMeta));
        Assert.False(
            tMeta.TryGetProperty("$lastUpdatedBy", out _),
            "$lastUpdatedBy should be stripped from nested twin T"
        );
        Assert.True(
            tMeta.TryGetProperty("$lastUpdateTime", out _),
            "$lastUpdateTime should remain in nested twin T metadata"
        );
        Assert.True(
            tMeta.TryGetProperty("temperature", out _),
            "per-property metadata should remain in nested twin T"
        );

        Assert.True(sTwin.TryGetProperty("$metadata", out JsonElement sMeta));
        Assert.False(
            sMeta.TryGetProperty("$lastUpdatedBy", out _),
            "$lastUpdatedBy should be stripped from nested twin S"
        );
        Assert.True(
            sMeta.TryGetProperty("$lastUpdateTime", out _),
            "$lastUpdateTime should remain in nested twin S metadata"
        );
        Assert.True(
            sMeta.TryGetProperty("$lastUpdateTime", out _),
            "$lastUpdateTime should remain in nested twin S metadata"
        );
        Assert.True(
            sMeta.TryGetProperty("temperature", out _),
            "per-property metadata should remain in nested twin S"
        );
    }

    private HttpRequestMessage CreatePutRelationshipRequest(string sourceId, string relationshipId, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/digitaltwins/{sourceId}/relationships/{relationshipId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-User-Id", TestUserId);
        return request;
    }

    private HttpRequestMessage CreateGetRelationshipRequest(string sourceId, string relationshipId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/digitaltwins/{sourceId}/relationships/{relationshipId}");
        request.Headers.Add("X-User-Id", TestUserId);
        return request;
    }

    [Fact]
    public async Task CreateRelationship_WithXUserIdHeader_ResponseHasLastUpdatedBy()
    {
        // Arrange - create source and target twins first
        var sourceJson = $$"""
            {
                "$dtId": "tlby-rel-create-src",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:room;1" },
                "name": "Source Room",
                "temperature": 22.0
            }
            """;
        var sourcePut = await _httpClient!.SendAsync(CreatePutRequest("tlby-rel-create-src", sourceJson));
        sourcePut.EnsureSuccessStatusCode();

        var targetJson = $$"""
            {
                "$dtId": "tlby-rel-create-tgt",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 25.0
            }
            """;
        var targetPut = await _httpClient!.SendAsync(CreatePutRequest("tlby-rel-create-tgt", targetJson));
        targetPut.EnsureSuccessStatusCode();

        var relBody = new JsonObject
        {
            ["$targetId"] = "tlby-rel-create-tgt",
            ["$relationshipName"] = "rel_has_sensors",
        };

        // Act
        var relPut = await _httpClient!.SendAsync(
            CreatePutRelationshipRequest("tlby-rel-create-src", "rel-create-test", relBody.ToJsonString())
        );
        var content = await relPut.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, relPut.StatusCode);

        var root = JsonDocument.Parse(content).RootElement;

        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata),
            "$metadata should exist on relationship");

        Assert.True(
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedBy),
            "$lastUpdatedBy should be inside relationship $metadata"
        );
        Assert.Equal(TestUserId, lastUpdatedBy.GetString());
    }

    [Fact]
    public async Task GetRelationship_AfterCreateWithUserId_LastUpdatedByInMetadata()
    {
        // Arrange
        var sourceJson = $$"""
            {
                "$dtId": "tlby-rel-get-src",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:room;1" },
                "name": "Source Room",
                "temperature": 22.0
            }
            """;
        var sourcePut = await _httpClient!.SendAsync(CreatePutRequest("tlby-rel-get-src", sourceJson));
        sourcePut.EnsureSuccessStatusCode();

        var targetJson = $$"""
            {
                "$dtId": "tlby-rel-get-tgt",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 25.0
            }
            """;
        var targetPut = await _httpClient!.SendAsync(CreatePutRequest("tlby-rel-get-tgt", targetJson));
        targetPut.EnsureSuccessStatusCode();

        var relBody = new JsonObject
        {
            ["$targetId"] = "tlby-rel-get-tgt",
            ["$relationshipName"] = "rel_has_sensors",
        };

        var relPut = await _httpClient!.SendAsync(
            CreatePutRelationshipRequest("tlby-rel-get-src", "rel-get-test", relBody.ToJsonString())
        );
        relPut.EnsureSuccessStatusCode();

        // Act
        var getResponse = await _httpClient!.SendAsync(
            CreateGetRelationshipRequest("tlby-rel-get-src", "rel-get-test")
        );
        var content = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var root = JsonDocument.Parse(content).RootElement;

        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata),
            "$metadata should exist on relationship");

        Assert.True(
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedBy),
            "$lastUpdatedBy should be inside relationship $metadata"
        );
        Assert.Equal(TestUserId, lastUpdatedBy.GetString());
    }

    [Fact]
    public async Task PatchRelationship_LastUpdatedByStillPresent()
    {
        // Arrange
        var sourceJson = $$"""
            {
                "$dtId": "tlby-rel-patch-src",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:room;1" },
                "name": "Source Room",
                "temperature": 22.0
            }
            """;
        var sourcePut = await _httpClient!.SendAsync(CreatePutRequest("tlby-rel-patch-src", sourceJson));
        sourcePut.EnsureSuccessStatusCode();

        var targetJson = $$"""
            {
                "$dtId": "tlby-rel-patch-tgt",
                "$metadata": { "$model": "dtmi:com:adt:dtsample:tempsensor;1" },
                "temperature": 25.0
            }
            """;
        var targetPut = await _httpClient!.SendAsync(CreatePutRequest("tlby-rel-patch-tgt", targetJson));
        targetPut.EnsureSuccessStatusCode();

        var relBody = new JsonObject
        {
            ["$targetId"] = "tlby-rel-patch-tgt",
            ["$relationshipName"] = "rel_has_sensors",
        };

        var relPut = await _httpClient!.SendAsync(
            CreatePutRelationshipRequest("tlby-rel-patch-src", "rel-patch-test", relBody.ToJsonString())
        );
        relPut.EnsureSuccessStatusCode();

        // Act - patch the relationship
        var patchBody = """[{ "op": "add", "path": "/temperature", "value": 23.5 }]""";
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch,
            "/digitaltwins/tlby-rel-patch-src/relationships/rel-patch-test")
        {
            Content = new StringContent(patchBody, Encoding.UTF8, "application/json-patch+json"),
        };
        patchRequest.Headers.Add("X-User-Id", TestUserId);
        var patchResponse = await _httpClient!.SendAsync(patchRequest);
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        // Assert - verify $lastUpdatedBy is still present
        var getResponse = await _httpClient!.SendAsync(
            CreateGetRelationshipRequest("tlby-rel-patch-src", "rel-patch-test")
        );
        var content = await getResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var root = JsonDocument.Parse(content).RootElement;

        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata),
            "$metadata should exist on relationship after patch");

        Assert.True(
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedBy),
            "$lastUpdatedBy should be inside relationship $metadata after patch"
        );
        Assert.Equal(TestUserId, lastUpdatedBy.GetString());
    }
}
