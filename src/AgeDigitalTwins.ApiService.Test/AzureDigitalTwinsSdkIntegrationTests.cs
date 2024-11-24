using Aspire.Hosting;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;

namespace AgeDigitalTwins.ApiService.Test;

public class AzureDigitalTwinsSdkIntegrationTests : IAsyncLifetime
{
    private DigitalTwinsClient? _digitalTwinsClient;
    private IDistributedApplicationTestingBuilder? _appHost;
    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        _appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AgeDigitalTwins_AppHost>();
        _appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });
        _app = await _appHost.BuildAsync();
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await _app.StartAsync();

        await resourceNotificationService.WaitForResourceAsync("apiservice", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var generatedHttpClient = _app.CreateHttpClient("apiservice");
        _httpClient = new HttpClient(new CustomHttpClientHandler(generatedHttpClient.BaseAddress!));

        DigitalTwinsClientOptions options = new()
        {
            Transport = new HttpClientTransport(_httpClient),
        };
        _digitalTwinsClient = new DigitalTwinsClient(
            new Uri("https://my-digital-twins-instance.com"),
            new CustomTokenCredential(),
            options);
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task CreateOrUpdateDigitalTwin_WithBasicDigitalTwinModelNotFound_ReturnsBadRequest()
    {
        // Arrange
        BasicDigitalTwin basicDigitalTwin = new()
        {
            Id = "myTwin",
            Metadata = new DigitalTwinMetadata
            {
                ModelId = "dtmi:com:example:Thermostat;1"
            },
            Contents = new Dictionary<string, object>
            {
                { "Temperature", 42 }
            }
        };

        // Act
        Assert.NotNull(_digitalTwinsClient);
        try
        {
            await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(basicDigitalTwin.Id, basicDigitalTwin);
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
        BasicDigitalTwin basicDigitalTwin = new()
        {
            Id = "myTwin",
            Metadata = new DigitalTwinMetadata
            {
                ModelId = "dtmi:com:adt:dtsample:tempsensor;1"
            },
            Contents = new Dictionary<string, object>
            {
                { "temperature", 42 }
            }
        };

        // Act
        Assert.NotNull(_digitalTwinsClient);
        await _digitalTwinsClient.CreateModelsAsync(new List<string> { SampleData.DtdlTemperatureSensor });
        BasicDigitalTwin newTwin = await _digitalTwinsClient.CreateOrReplaceDigitalTwinAsync(basicDigitalTwin.Id, basicDigitalTwin);

        // Assert
        Assert.Equal(newTwin.Id, basicDigitalTwin.Id);
    }

    public class CustomTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, System.Threading.CancellationToken cancellationToken)
        {
            return new AccessToken("fake-token", DateTimeOffset.MaxValue);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, System.Threading.CancellationToken cancellationToken)
        {
            return new ValueTask<AccessToken>(new AccessToken("fake-token", DateTimeOffset.MaxValue));
        }
    }


    public class CustomHttpClientHandler : HttpClientHandler
    {
        private readonly Uri _baseAddress;

        public CustomHttpClientHandler(Uri baseAddress)
        {
            _baseAddress = baseAddress;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            request.RequestUri = new Uri(_baseAddress, request.RequestUri!.PathAndQuery);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
