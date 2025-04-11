using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;

namespace AgeDigitalTwins.ApiService.Test;

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
}
