using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgeDigitalTwins.Models;

namespace AgeDigitalTwins.ApiService.Test;

[Trait("Category", "Integration")]
public class ModelsIntegrationTests : IAsyncLifetime
{
    private TestingAspireAppHost? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        _app = new TestingAspireAppHost();
        await _app.StartAsync();
        _httpClient = _app.CreateHttpClient("apiservice");
    }

    public async Task DisposeAsync()
    {
        var response = await _httpClient!.DeleteAsync("/graph/delete");
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task CreateModelsDeleteModel_SingleModel_ValidatedCreatedDeleted()
    {
        // Arrange
        string[] sModels = [SampleData.DtdlCrater];
        List<JsonElement> jModels = sModels
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();

        // Act
        var createResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json")
        );
        var deleteResponse = await _httpClient!.DeleteAsync("/models/dtmi:com:contoso:Crater;1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GetModels_SingleModel_ValidatedAndCreated()
    {
        // Arrange
        string[] sModels = [SampleData.DtdlCrater];
        List<JsonElement> jModels = sModels
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();

        // Act
        var response = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json")
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _httpClient!.GetAsync("/models");
        getResponse.EnsureSuccessStatusCode();
        string getResponseContent = await getResponse.Content.ReadAsStringAsync();
        JsonDocument getResponseJson = JsonDocument.Parse(getResponseContent);
        var results = getResponseJson.RootElement.GetProperty("value").EnumerateArray().ToList();
    }

    [Fact]
    public async Task CreateModels_MultipleDependentModelsResolveInDb_ValidatedAndCreated()
    {
        // Clean up any existing models
        try
        {
            var delres1 = await _httpClient!.DeleteAsync(
                "/models/dtmi:com:contoso:CelestialBody;1"
            );
            delres1.EnsureSuccessStatusCode();
        }
        catch { }
        try
        {
            var delres2 = await _httpClient!.DeleteAsync("/models/dtmi:com:contoso:Planet;1");
            delres2.EnsureSuccessStatusCode();
        }
        catch { }
        try
        {
            var delres3 = await _httpClient!.DeleteAsync("/models/dtmi:com:contoso:Crater;1");
            delres3.EnsureSuccessStatusCode();
        }
        catch { }

        // Arrange
        string[] sModels = [SampleData.DtdlCelestialBody, SampleData.DtdlCrater];
        List<JsonElement> jModels = sModels
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();

        string[] sModels2 = [SampleData.DtdlPlanet];
        List<JsonElement> jModels2 = sModels2
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();

        // Act
        var response = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json")
        );
        response.EnsureSuccessStatusCode();
        string responseContent = await response.Content.ReadAsStringAsync();
        var response2 = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels2), Encoding.UTF8, "application/json")
        );
        response2.EnsureSuccessStatusCode();
        string response2Content = await response2.Content.ReadAsStringAsync();
        List<DigitalTwinsModelData> results = JsonSerializer.Deserialize<
            List<DigitalTwinsModelData>
        >(response2Content)!;

        for (int i = 0; i < jModels2.Count; i++)
        {
            var resultJson = JsonDocument.Parse(results[i].DtdlModel!);
            var sampleDataJson = jModels2[i];

            var resultId = resultJson.RootElement.GetProperty("@id").GetString();
            var sampleDataId = sampleDataJson.GetProperty("@id").GetString();

            Assert.Equal(sampleDataId, results[i].Id);
            Assert.Equal(sampleDataId, resultId);
        }
    }
}
