using Aspire.Hosting;
using System.Text.Json;
using System.Text;

namespace AgeDigitalTwins.ApiService.Test;

public class ModelsIntegrationTests : IAsyncLifetime
{
    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AgeDigitalTwins_AppHost>();
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });
        _app = await appHost.BuildAsync();

        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await _app.StartAsync();

        await resourceNotificationService.WaitForResourceAsync("apiservice", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        _httpClient = _app.CreateHttpClient("apiservice");
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
    public async Task Health()
    {
        // Act
        var response = await _httpClient!.GetAsync(
            "/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateModels_SingleModel_ValidatedAndCreated()
    {
        // Arrange
        string[] models = [SampleData.DtdlRoom];


        // Act
        var response = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(models), Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
