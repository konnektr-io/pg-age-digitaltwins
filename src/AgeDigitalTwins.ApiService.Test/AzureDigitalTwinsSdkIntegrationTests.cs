
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;

namespace AgeDigitalTwins.ApiService.Test;

[Trait("Category", "Integration")]
public class AzureDigitalTwinsSdkIntegrationTests : IAsyncLifetime
{
    private DigitalTwinsClient? _digitalTwinsClient;
    private TestingAspireAppHost? _app;
    private HttpClient? _generatedhttpClient;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        _app = new TestingAspireAppHost();
        await _app.StartAsync();
        Console.WriteLine("App started");

        _generatedhttpClient = _app.CreateHttpClient("apiservice");
        _httpClient = new HttpClient(
            new CustomHttpClientHandler(_generatedhttpClient.BaseAddress!)
        );

        DigitalTwinsClientOptions options =
            new() { Transport = new HttpClientTransport(_httpClient) };
        _digitalTwinsClient = new DigitalTwinsClient(
            new Uri("https://my-digital-twins-instance.com"),
            new CustomTokenCredential(),
            options
        );
    }

    public async Task DisposeAsync()
    {
        var response = await _generatedhttpClient!.DeleteAsync("/graph/delete");
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
        _httpClient?.Dispose();
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

    private class CustomHttpClientHandler(Uri baseAddress) : HttpClientHandler
    {
        private readonly Uri _baseAddress = baseAddress;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            request.RequestUri = new Uri(_baseAddress, request.RequestUri!.PathAndQuery);
            return await base.SendAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task CreateOrUpdateDigitalTwin_WithBasicDigitalTwinModelNotFound_ReturnsBadRequest()
    {
        // Arrange
        BasicDigitalTwin basicDigitalTwin =
            new()
            {
                Id = "myTwin",
                Metadata = new DigitalTwinMetadata { ModelId = "dtmi:com:example:Thermostat;1" },
                Contents = new Dictionary<string, object> { { "Temperature", 42 } },
            };

        // Act
        Assert.NotNull(_digitalTwinsClient);
        try
        {
            await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(
                basicDigitalTwin.Id,
                basicDigitalTwin
            );
        }
        catch (RequestFailedException ex)
        {
            // Assert
            Assert.Equal(400, ex.Status);
        }
    }

    [Fact]
    public async Task CreateOrUpdateDigitalTwin_WithBasicDigitalTwin_ReturnsTwin()
    {
        // Arrange
        BasicDigitalTwin basicDigitalTwin =
            new()
            {
                Id = "myTwin",
                Metadata = new DigitalTwinMetadata
                {
                    ModelId = "dtmi:com:adt:dtsample:tempsensor;1",
                },
                Contents = new Dictionary<string, object> { { "temperature", 42 } },
            };

        // Act
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            new List<string> { SampleData.DtdlTemperatureSensor }
        );
        BasicDigitalTwin newTwin = await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(
            basicDigitalTwin.Id,
            basicDigitalTwin
        );

        // Assert
        Assert.Equal(newTwin.Id, basicDigitalTwin.Id);
    }

    [Fact]
    public async Task Query_WithSimpleQuery_ReturnsResult()
    {
        // Arrange
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(new List<string> { SampleData.DtdlCrater });
        var crater = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater)!;
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(crater.Id, crater);
        string query = "SELECT * FROM digitaltwins";

        // Act
        Assert.NotNull(_digitalTwinsClient);
        bool found = false;
        await foreach (
            BasicDigitalTwin twin in _digitalTwinsClient.QueryAsync<BasicDigitalTwin>(query)
        )
        {
            // Assert
            Assert.NotNull(twin);
            if (twin.Id == crater.Id)
            {
                found = true;
            }
        }
        Assert.True(found);
    }

    [Fact]
    public async Task ListRelationships_WithValidId_ReturnsRelationships()
    {
        // Arrange
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            new List<string>
            {
                SampleData.DtdlCelestialBody,
                SampleData.DtdlPlanet,
                SampleData.DtdlMoon,
                SampleData.DtdlCrater,
            }
        );
        var earth = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinPlanetEarth)!;
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(earth.Id, earth);
        var moon = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinMoonLuna)!;
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(moon.Id, moon);
        string relationshipName = "satellites";

        string relationshipId = "myRelationshipId";
        var relationship = new BasicRelationship()
        {
            Id = relationshipId,
            TargetId = moon.Id,
            SourceId = earth.Id,
            Name = relationshipName,
        };
        await _digitalTwinsClient.CreateOrReplaceRelationshipAsync(
            earth.Id,
            relationshipId,
            relationship
        );

        // Act
        Assert.NotNull(_digitalTwinsClient);
        AsyncPageable<BasicRelationship> relationships =
            _digitalTwinsClient.GetRelationshipsAsync<BasicRelationship>(earth.Id);

        // Assert
        bool found = false;
        await foreach (BasicRelationship rel in relationships)
        {
            if (rel.Id == relationshipId)
            {
                found = true;
            }
        }
        Assert.True(found);
    }

    [Fact]
    public async Task GetModels_WithValidModel_ReturnsModelDefinitions()
    {
        // Arrange
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            new List<string> { SampleData.DtdlCelestialBody, SampleData.DtdlCrater }
        );
        string modelId = "dtmi:com:contoso:Crater;1";

        // Act
        Assert.NotNull(_digitalTwinsClient);
        AsyncPageable<DigitalTwinsModelData> models = _digitalTwinsClient.GetModelsAsync(
            new() { IncludeModelDefinition = true }
        );

        // Assert
        bool found = false;
        await foreach (DigitalTwinsModelData model in models)
        {
            Assert.NotNull(model);
            Assert.NotNull(model.DtdlModel);
            if (model.Id == modelId)
            {
                found = true;
            }
        }
        Assert.True(found);
    }

    [Fact]
    public async Task Query_SupportsPagination()
    {
        // Arrange
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(new List<string> { SampleData.DtdlCrater });

        var crater1 = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater)!;
        crater1.Id = "crater1";
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(crater1.Id, crater1);

        var crater2 = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater)!;
        crater2.Id = "crater2";
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(crater2.Id, crater2);

        var crater3 = JsonSerializer.Deserialize<BasicDigitalTwin>(SampleData.TwinCrater)!;
        crater3.Id = "crater3";
        await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(crater3.Id, crater3);

        string query = "SELECT * FROM digitaltwins";

        // Act
        Assert.NotNull(_digitalTwinsClient);

        // Assert
        int twinCount = 0;
        int pageCount = 0;
        await foreach (
            Page<BasicDigitalTwin> page in _digitalTwinsClient
                .QueryAsync<BasicDigitalTwin>(query)
                .AsPages(pageSizeHint: 1)
        )
        {
            Assert.NotNull(page);
            twinCount += page.Values.Count;
            pageCount++;
        }
        Assert.True(pageCount > 1, "Expected multiple pages of results but found only one page.");
        Assert.Equal(3, twinCount);
    }

    [Fact]
    public async Task CreateAndGetDigitalTwin_WithPercentEncodedId_WorksCorrectly()
    {
        // Arrange
        string twinId = "10%B2H6_H2";
        BasicDigitalTwin basicDigitalTwin =
            new()
            {
                Id = twinId,
                Metadata = new DigitalTwinMetadata { ModelId = "dtmi:com:adt:dtsample:tempsensor;1" },
                Contents = new Dictionary<string, object> { { "temperature", 42 } },
            };

        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            new List<string> { SampleData.DtdlTemperatureSensor }
        );

        // Act: Create the twin
        BasicDigitalTwin newTwin = await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(
            basicDigitalTwin.Id,
            basicDigitalTwin
        );
        Assert.Equal(newTwin.Id, basicDigitalTwin.Id);

        // Act: Get the twin
        BasicDigitalTwin fetchedTwin = await _digitalTwinsClient.GetDigitalTwinAsync<BasicDigitalTwin>(twinId);
        Assert.NotNull(fetchedTwin);
        Assert.Equal(twinId, fetchedTwin.Id);
        Assert.True(fetchedTwin.Contents.ContainsKey("temperature"));
        Assert.Equal(42, ((JsonElement)fetchedTwin.Contents["temperature"]).GetInt32());
    }
    
    [Fact]
    public async Task CreateAndGetDigitalTwin_VerifiesEtagAndLastUpdateTime()
    {
        // Arrange
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(
            new List<string> { SampleData.DtdlTemperatureSensor }
        );

        string twinId = "testTwinEtag";
        BasicDigitalTwin basicDigitalTwin =
            new()
            {
                Id = twinId,
                Metadata = new DigitalTwinMetadata
                {
                    ModelId = "dtmi:com:adt:dtsample:tempsensor;1",
                },
                Contents = new Dictionary<string, object> { { "temperature", 42 } },
            };

        // Act 1: Create
        BasicDigitalTwin createdTwin = await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(
            twinId,
            basicDigitalTwin
        );

        // Assert 1: The returned twin should have a LastUpdateTime and an Etag
        Assert.NotNull(createdTwin.ETag);
        Assert.NotNull(createdTwin.LastUpdatedOn);

        // Act 2: Get the same twin with BasicDigitalTwin as a return type
        BasicDigitalTwin fetchedBasicTwin =
            await _digitalTwinsClient.GetDigitalTwinAsync<BasicDigitalTwin>(twinId);

        // Assert 2: LastUpdateTime and Etag should be the same
        Assert.Equal(createdTwin.ETag, fetchedBasicTwin.ETag);
        Assert.Equal(createdTwin.LastUpdatedOn, fetchedBasicTwin.LastUpdatedOn);

        // Act 3: Get the same twin with JsonDocument as a return type
        Response<JsonDocument> response = await _digitalTwinsClient.GetDigitalTwinAsync<JsonDocument>(
            twinId
        );
        JsonElement root = response.Value.RootElement;

        // Assert 3: The $etag should be the same
        Assert.True(root.TryGetProperty("$etag", out JsonElement etagProp));
        Assert.Equal(createdTwin.ETag.ToString(), etagProp.GetString());

        // Assert 4: The $lastUpdateTime should be in $metadata.$lastUpdateTime and should have the same value
        Assert.True(root.TryGetProperty("$metadata", out JsonElement metadataProp));
        Assert.True(metadataProp.TryGetProperty("$lastUpdateTime", out JsonElement lastUpdateProp));

        DateTimeOffset actualLastUpdate = DateTimeOffset.Parse(lastUpdateProp.GetString()!);
        Assert.Equal(createdTwin.LastUpdatedOn.Value, actualLastUpdate);
    }
}
