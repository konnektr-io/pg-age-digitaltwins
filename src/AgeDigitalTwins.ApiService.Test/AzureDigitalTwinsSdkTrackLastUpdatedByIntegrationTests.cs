using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;

namespace AgeDigitalTwins.ApiService.Test;

/// <summary>
/// App host with <c>TrackLastUpdatedBy</c> and <c>UserIdHeaderName</c> enabled.
/// These environment variables flow into the API service configuration.
/// </summary>
public class TrackLastUpdatedByAppHost : DistributedApplicationFactory
{
    public TrackLastUpdatedByAppHost()
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
public class AzureDigitalTwinsSdkTrackLastUpdatedByIntegrationTests : IAsyncLifetime
{
    private const string TestUserId = "sdk-test-user";

    private TrackLastUpdatedByAppHost? _app;
    private DigitalTwinsClient? _digitalTwinsClient;
    private HttpClient? _generatedHttpClient;

    public async Task InitializeAsync()
    {
        _app = new TrackLastUpdatedByAppHost();
        await _app.StartAsync();

        _generatedHttpClient = _app.CreateHttpClient("apiservice");
        var httpClient = new HttpClient(
            new UserIdInjectingHandler(_generatedHttpClient.BaseAddress!)
        );

        DigitalTwinsClientOptions options =
            new() { Transport = new HttpClientTransport(httpClient) };
        _digitalTwinsClient = new DigitalTwinsClient(
            new Uri("https://my-digital-twins-instance.com"),
            new CustomTokenCredential(),
            options
        );
    }

    public async Task DisposeAsync()
    {
        if (_generatedHttpClient != null)
        {
            await _generatedHttpClient.DeleteAsync("/graph/delete");
        }
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    private class UserIdInjectingHandler(Uri baseAddress) : HttpClientHandler
    {
        private readonly Uri _baseAddress = baseAddress;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            request.Headers.Remove("X-User-Id");
            request.Headers.Add("X-User-Id", TestUserId);
            request.RequestUri = new Uri(_baseAddress, request.RequestUri!.PathAndQuery);
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private class CustomTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        )
        {
            return new AccessToken("fake-token", DateTimeOffset.MaxValue);
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        )
        {
            return new ValueTask<AccessToken>(
                new AccessToken("fake-token", DateTimeOffset.MaxValue)
            );
        }
    }

    [Fact]
    public async Task GetTwinAsJsonDocument_LastUpdatedByInMetadata()
    {
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            [SampleData.DtdlTemperatureSensor]
        );

        string twinId = "tlby-json-doc";
        var twin = new BasicDigitalTwin
        {
            Id = twinId,
            Metadata = new DigitalTwinMetadata
            {
                ModelId = "dtmi:com:adt:dtsample:tempsensor;1",
            },
            Contents = new Dictionary<string, object> { { "temperature", 22 } },
        };
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(twinId, twin);

        // Get as JsonDocument to inspect raw JSON
        Response<JsonDocument> response =
            await _digitalTwinsClient.GetDigitalTwinAsync<JsonDocument>(twinId);
        JsonElement root = response.Value.RootElement;

        // $lastUpdatedBy should be inside $metadata, not at root
        Assert.False(
            root.TryGetProperty("$lastUpdatedBy", out _),
            "$lastUpdatedBy should not be at root level"
        );
        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata));
        Assert.True(
            metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedByProp),
            "$lastUpdatedBy should be inside $metadata"
        );
        Assert.Equal(TestUserId, lastUpdatedByProp.GetString());

        // $lastUpdateTime should also be in $metadata
        Assert.True(metadata.TryGetProperty("$lastUpdateTime", out JsonElement lastUpdateProp));
        Assert.NotNull(lastUpdateProp.GetString());
        Assert.True(DateTimeOffset.TryParse(lastUpdateProp.GetString(), out _));

        // Per-property lastUpdatedBy should be in $metadata.temperature
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
            tempMetadata.TryGetProperty("lastUpdateTime", out JsonElement tempLastUpdateTime),
            "temperature.lastUpdateTime should exist"
        );
        Assert.True(DateTimeOffset.TryParse(tempLastUpdateTime.GetString(), out _));
    }

    [Fact]
    public async Task GetTwinAsBasicDigitalTwin_OnTrackLastUpdatedBy_Throws()
    {
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            [SampleData.DtdlTemperatureSensor]
        );

        string twinId = "tlby-basic-fail";
        var twin = new BasicDigitalTwin
        {
            Id = twinId,
            Metadata = new DigitalTwinMetadata
            {
                ModelId = "dtmi:com:adt:dtsample:tempsensor;1",
            },
            Contents = new Dictionary<string, object> { { "temperature", 18 } },
        };
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(twinId, twin);

        // Creating via BasicDigitalTwin works (the request body doesn't have $lastUpdatedBy)
        // But getting back as BasicDigitalTwin fails because the response has $lastUpdatedBy in $metadata
        await Assert.ThrowsAsync<RequestFailedException>(() =>
            _digitalTwinsClient.GetDigitalTwinAsync<BasicDigitalTwin>(twinId));
    }

    [Fact]
    public async Task GetTwinWithCustomDto_WorksCorrectly()
    {
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            [SampleData.DtdlTemperatureSensor]
        );

        string twinId = "tlby-custom-dto";
        var twin = new BasicDigitalTwin
        {
            Id = twinId,
            Metadata = new DigitalTwinMetadata
            {
                ModelId = "dtmi:com:adt:dtsample:tempsensor;1",
            },
            Contents = new Dictionary<string, object> { { "temperature", 25 } },
        };
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(twinId, twin);

        // Custom DTOs work because they don't try to parse $metadata
        Response<JsonObject> fetchedResponse =
            await _digitalTwinsClient.GetDigitalTwinAsync<JsonObject>(twinId);
        JsonObject fetched = fetchedResponse.Value;
        Assert.NotNull(fetched);
        Assert.Equal(twinId, fetched["$dtId"]?.ToString());
        Assert.NotNull(fetched["$metadata"]);
        Assert.Equal("25", fetched["temperature"]?.ToString());

        // Verify $lastUpdatedBy is in $metadata (twin-level)
        var metadata = fetched["$metadata"]!.AsObject();
        Assert.True(metadata.ContainsKey("$lastUpdatedBy"));
        Assert.Equal(TestUserId, metadata["$lastUpdatedBy"]!.GetValue<string>());

        // Verify per-property lastUpdatedBy is in $metadata.temperature
        Assert.True(metadata.ContainsKey("temperature"), "per-property metadata for temperature should exist");
        var tempMetadata = metadata["temperature"]!.AsObject();
        Assert.True(tempMetadata.ContainsKey("lastUpdatedBy"), "temperature.lastUpdatedBy should exist");
        Assert.Equal(TestUserId, tempMetadata["lastUpdatedBy"]!.GetValue<string>());
        Assert.True(tempMetadata.ContainsKey("lastUpdateTime"), "temperature.lastUpdateTime should exist");
    }

    [Fact]
    public async Task PatchTwinAndVerifyLastUpdatedByInMetadata()
    {
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            [SampleData.DtdlTemperatureSensor]
        );

        string twinId = "tlby-patch-test";
        var twin = new BasicDigitalTwin
        {
            Id = twinId,
            Metadata = new DigitalTwinMetadata
            {
                ModelId = "dtmi:com:adt:dtsample:tempsensor;1",
            },
            Contents = new Dictionary<string, object> { { "temperature", 20 } },
        };
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(twinId, twin);

        // Patch the twin
        var patch = new JsonPatchDocument();
        patch.AppendReplace("/temperature", 26);
        await _digitalTwinsClient.UpdateDigitalTwinAsync(twinId, patch);

        // Verify with JsonDocument
        Response<JsonDocument> response =
            await _digitalTwinsClient.GetDigitalTwinAsync<JsonDocument>(twinId);
        JsonElement root = response.Value.RootElement;
        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadata));

        // Twin-level $lastUpdatedBy
        Assert.True(metadata.TryGetProperty("$lastUpdatedBy", out JsonElement lastUpdatedByProp));
        Assert.Equal(TestUserId, lastUpdatedByProp.GetString());

        // Per-property lastUpdatedBy
        Assert.True(
            metadata.TryGetProperty("temperature", out JsonElement tempMetadata),
            "per-property metadata for temperature should exist"
        );
        Assert.True(
            tempMetadata.TryGetProperty("lastUpdatedBy", out JsonElement tempLastUpdatedBy),
            "temperature.lastUpdatedBy should exist"
        );
        Assert.Equal(TestUserId, tempLastUpdatedBy.GetString());

        // temperature value should be updated
        Assert.True(root.TryGetProperty("temperature", out JsonElement tempProp));
        Assert.Equal(26, tempProp.GetInt32());
    }
}
