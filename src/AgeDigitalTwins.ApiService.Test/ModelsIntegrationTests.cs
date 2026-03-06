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
        var getResponse = await _httpClient!.GetAsync("/models");
        getResponse.EnsureSuccessStatusCode();

        string getResponseContent = await getResponse.Content.ReadAsStringAsync();
        JsonDocument getResponseJson = JsonDocument.Parse(getResponseContent);
        var results = getResponseJson.RootElement.GetProperty("value").EnumerateArray().ToList();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(results.Count > 0, "No models found in the response.");
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

    [Fact]
    public async Task GetModels_WithIncludeModelDefinition_ReturnsFullModelContent()
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
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await _httpClient!.GetAsync(
            "/models?api-version=2022-05-31&dependenciesFor=&includeModelDefinition=true"
        );
        getResponse.EnsureSuccessStatusCode();

        string getResponseContent = await getResponse.Content.ReadAsStringAsync();
        JsonDocument getResponseJson = JsonDocument.Parse(getResponseContent);
        var results = getResponseJson.RootElement.GetProperty("value").EnumerateArray().ToList();

        // Assert
        Assert.True(results.Count > 0, "No models found in the response.");
        foreach (var model in results)
        {
            Assert.True(
                model.TryGetProperty("model", out _),
                "Model content is not included in the response."
            );
        }
    }

    [Fact]
    public async Task GetModel_WithIncludeBaseModelContents_ReturnsPropertiesFieldWithContent()
    {
        string[] sModels = [SampleData.DtdlRoom];
        List<JsonElement> jModels = sModels
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();
        var createResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json")
        );
        createResponse.EnsureSuccessStatusCode();

        // Act: Call the API with includeBaseModelContents=true
        var getResponse = await _httpClient!.GetAsync(
            "/models/dtmi:com:adt:dtsample:room;1?includeBaseModelContents=true"
        );
        getResponse.EnsureSuccessStatusCode();
        string getResponseContent = await getResponse.Content.ReadAsStringAsync();
        JsonDocument getResponseJson = JsonDocument.Parse(getResponseContent);
        var root = getResponseJson.RootElement;

        // Assert: properties field exists and contains expected property names
        Assert.True(
            root.TryGetProperty("properties", out var properties),
            "properties field missing"
        );
        Assert.Equal(JsonValueKind.Array, properties.ValueKind);
        var propNames = properties
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("name", propNames);
        Assert.Contains("temperature", propNames);
        // Should not contain duplicates
        Assert.Equal(propNames.Distinct().Count(), propNames.Count);
    }

    [Fact]
    public async Task GetModels_Pagination_FirstPageHasNextLinkWithContinuationToken()
    {
        // Arrange - create multiple models
        string[] sModels =
        [
            SampleData.DtdlCrater,
            SampleData.DtdlCelestialBody,
            SampleData.DtdlPlanet,
        ];
        List<JsonElement> jModels = sModels
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();
        var createResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json")
        );
        createResponse.EnsureSuccessStatusCode();

        // Act - request first page with a limit of 1 item
        var request = new HttpRequestMessage(HttpMethod.Get, "/models");
        request.Headers.Add("max-items-per-page", "1");
        var getResponse = await _httpClient!.SendAsync(request);
        getResponse.EnsureSuccessStatusCode();

        string content = await getResponse.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(content);
        var results = json.RootElement.GetProperty("value").EnumerateArray().ToList();

        // Assert - first page has exactly 1 item and a nextLink containing a continuationToken
        Assert.Single(results);
        Assert.True(
            json.RootElement.TryGetProperty("nextLink", out var nextLinkEl),
            "Expected nextLink to be present in paginated response but it was missing."
        );
        string nextLink = nextLinkEl.GetString()!;
        Assert.Contains("continuationToken=", nextLink);
    }

    [Fact]
    public async Task GetModels_Pagination_AllModelsReturnedAcrossPages()
    {
        // Arrange - create 3 models
        string[] sModels =
        [
            SampleData.DtdlCrater,
            SampleData.DtdlCelestialBody,
            SampleData.DtdlPlanet,
        ];
        List<JsonElement> jModels = sModels
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();
        var createResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json")
        );
        createResponse.EnsureSuccessStatusCode();

        // Act - paginate through all pages, 1 item per page
        var allModelIds = new List<string>();
        string? requestUri = "/models";
        int pageCount = 0;

        while (requestUri != null)
        {
            var relativeUri = requestUri.StartsWith("http")
                ? new Uri(requestUri).PathAndQuery
                : requestUri;
            var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
            request.Headers.Add("max-items-per-page", "1");
            var response = await _httpClient!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string pageContent = await response.Content.ReadAsStringAsync();
            JsonDocument pageJson = JsonDocument.Parse(pageContent);

            allModelIds.AddRange(
                pageJson
                    .RootElement.GetProperty("value")
                    .EnumerateArray()
                    .Select(r => r.GetProperty("id").GetString()!)
            );
            pageCount++;

            requestUri = pageJson.RootElement.TryGetProperty("nextLink", out var nextLinkEl)
                ? nextLinkEl.GetString()
                : null;
        }

        // Assert - all 3 models are returned across multiple pages
        Assert.True(pageCount > 1, $"Expected multiple pages but got only {pageCount}.");
        Assert.Contains("dtmi:com:contoso:Crater;1", allModelIds);
        Assert.Contains("dtmi:com:contoso:CelestialBody;1", allModelIds);
        Assert.Contains("dtmi:com:contoso:Planet;1", allModelIds);
    }
}
