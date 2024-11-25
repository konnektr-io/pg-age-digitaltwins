using Aspire.Hosting;
using System.Text.Json;
using System.Text;

namespace AgeDigitalTwins.ApiService.Test;

public class DigitalTwinsIntegrationTests : IAsyncLifetime
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

        string[] sModels = [
            SampleData.DtdlRoom, SampleData.DtdlTemperatureSensor, SampleData.DtdlCelestialBody, SampleData.DtdlPlanet, SampleData.DtdlCrater];
        List<JsonElement> jModels = sModels.Select(m => JsonDocument.Parse(m)).Select(j => j.RootElement).ToList();

        var modelResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json"));
        string modelResponseContent = await modelResponse.Content.ReadAsStringAsync();
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
    public async Task CreateOrUpdateDigitalTwin_ValidTwin_ReturnsCreatedTwin()
    {
        var twinResponse = await _httpClient!.PutAsync(
            "/digitaltwins/crater1",
            new StringContent(SampleData.TwinCrater, Encoding.UTF8, "application/json"));
        string twinResponseContent = await twinResponse.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, twinResponse.StatusCode);

        JsonDocument twinJson = JsonDocument.Parse(twinResponseContent);
        Assert.Equal("crater1", twinJson.RootElement.GetProperty("$dtId").GetString());
    }


    [Fact]
    public async Task CreateOrUpdateDigitalTwin_InvalidTwin_ReturnsValidationFailed()
    {
        var twinResponse = await _httpClient!.PutAsync(
            "/digitaltwins/crater1",
            new StringContent(@"{
                ""$dtId"": ""crater1"",
                ""$metadata"": {
                    ""$model"": ""dtmi:com:contoso:Crater;1""
                },
                ""badproperty"": 100
            }", Encoding.UTF8, "application/json"));
        string twinResponseContent = await twinResponse.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, twinResponse.StatusCode);
        JsonDocument responseJson = JsonDocument.Parse(twinResponseContent);
        Assert.Equal("ValidationFailedException", responseJson.RootElement.GetProperty("type").GetString());
        Assert.True(responseJson.RootElement.TryGetProperty("detail", out JsonElement messageElement));
        Assert.Equal(JsonValueKind.String, messageElement.ValueKind);
        Assert.Contains("badproperty", messageElement.GetString());
    }
}
