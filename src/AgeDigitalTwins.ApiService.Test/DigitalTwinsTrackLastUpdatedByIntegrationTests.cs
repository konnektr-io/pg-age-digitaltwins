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
        var apiService = applicationBuilder.Resources.FirstOrDefault(r => r.Name == "apiservice");
        if (apiService is IResourceWithEnvironment resource)
        {
            resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["Parameters:TrackLastUpdatedBy"] = "true";
                context.EnvironmentVariables["Parameters:UserIdHeaderName"] = "X-User-Id";
            }));
        }

        applicationBuilder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });
    }
}

[Trait("Category", "Integration")]
public class DigitalTwinsTrackLastUpdatedByIntegrationTests : IAsyncLifetime
{
    private const string TestUserId = "integration-test-user";

    private TrackLastUpdatedByAppHostForHttp? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
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
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedBy),
            "$lastUpdatedBy should be inside $metadata"
        );
        Assert.Equal(TestUserId, lastUpdatedBy.GetString());

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
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedBy),
            "$lastUpdatedBy should be inside $metadata"
        );
        Assert.Equal(TestUserId, lastUpdatedBy.GetString());

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
