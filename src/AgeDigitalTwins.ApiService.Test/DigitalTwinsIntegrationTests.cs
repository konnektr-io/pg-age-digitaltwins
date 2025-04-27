using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;

namespace AgeDigitalTwins.ApiService.Test;

[Trait("Category", "Integration")]
public class DigitalTwinsIntegrationTests : IAsyncLifetime
{
    private TestingAspireAppHost? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        _app = new TestingAspireAppHost();
        await _app.StartAsync();
        _httpClient = _app.CreateHttpClient("apiservice");

        string[] sModels =
        [
            SampleData.DtdlRoom,
            SampleData.DtdlTemperatureSensor,
            SampleData.DtdlCelestialBody,
            SampleData.DtdlPlanet,
            SampleData.DtdlCrater,
        ];
        List<JsonElement> jModels = sModels
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();

        var modelResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json")
        );
        string modelResponseContent = await modelResponse.Content.ReadAsStringAsync();
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
    public async Task CreateOrUpdateDigitalTwin_ValidTwin_ReturnsCreatedTwin()
    {
        var twinResponse = await _httpClient!.PutAsync(
            "/digitaltwins/crater1",
            new StringContent(SampleData.TwinCrater, Encoding.UTF8, "application/json")
        );
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
            new StringContent(
                @"{
                ""$dtId"": ""crater1"",
                ""$metadata"": {
                    ""$model"": ""dtmi:com:contoso:Crater;1""
                },
                ""badproperty"": 100
            }",
                Encoding.UTF8,
                "application/json"
            )
        );
        string twinResponseContent = await twinResponse.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, twinResponse.StatusCode);
        JsonDocument responseJson = JsonDocument.Parse(twinResponseContent);
        Assert.Equal(
            "ValidationFailedException",
            responseJson.RootElement.GetProperty("type").GetString()
        );
        Assert.True(
            responseJson.RootElement.TryGetProperty("detail", out JsonElement messageElement)
        );
        Assert.Equal(JsonValueKind.String, messageElement.ValueKind);
        Assert.Contains("badproperty", messageElement.GetString());
    }

    [Fact]
    public async Task UpdateDigitalTwin_ValidTwin_UpdatesTwin()
    {
        var twinResponse = await _httpClient!.PutAsync(
            "/digitaltwins/earth",
            new StringContent(SampleData.TwinPlanetEarth, Encoding.UTF8, "application/json")
        );
        string twinResponseContent = await twinResponse.Content.ReadAsStringAsync();

        string jsonPatch =
            @"[
            {
                ""op"": ""replace"",
                ""path"": ""/name"",
                ""value"": ""Earth 2""
            },
            {
                ""op"": ""add"",
                ""path"": ""/mass"",
                ""value"": 5.972E18
            }
        ]";

        var patchResponse = await _httpClient!.PatchAsync(
            "/digitaltwins/earth",
            new StringContent(jsonPatch, Encoding.UTF8, "application/json-patch+json")
        );

        var modifiedTwinResponse = await _httpClient!.GetAsync("/digitaltwins/earth");

        // Assert
        Assert.Equal(HttpStatusCode.OK, twinResponse.StatusCode);
        JsonDocument twinJson = JsonDocument.Parse(twinResponseContent);
        Assert.Equal("earth", twinJson.RootElement.GetProperty("$dtId").GetString());

        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        Assert.Equal(HttpStatusCode.OK, modifiedTwinResponse.StatusCode);
        string modifiedTwinResponseContent = await modifiedTwinResponse.Content.ReadAsStringAsync();
        JsonDocument modifiedTwinJson = JsonDocument.Parse(modifiedTwinResponseContent);
        Assert.Equal("Earth 2", modifiedTwinJson.RootElement.GetProperty("name").GetString());
        Assert.Equal(5.972E18, modifiedTwinJson.RootElement.GetProperty("mass").GetDouble());
    }

    [Fact]
    public async Task Query_WithSimpleQuery_ReturnsResult()
    {
        // Arrange

        try
        {
            var delres3 = await _httpClient!.DeleteAsync("/models/dtmi:com:contoso:Crater;1");
            delres3.EnsureSuccessStatusCode();
        }
        catch { }
        string[] sModels = [SampleData.DtdlCrater];
        List<JsonElement> jModels = sModels
            .Select(m => JsonDocument.Parse(m))
            .Select(j => j.RootElement)
            .ToList();
        var createModelsResponse = await _httpClient!.PostAsync(
            "/models",
            new StringContent(JsonSerializer.Serialize(jModels), Encoding.UTF8, "application/json")
        );
        var createTwinResponse = _httpClient!.PutAsync(
            "/digitaltwins/crater1",
            new StringContent(SampleData.TwinCrater, Encoding.UTF8, "application/json")
        );

        // Wait for a few seconds to ensure the twin is created
        await Task.Delay(2000);

        // Act
        string query = "SELECT * FROM digitaltwins";
        JsonObject queryBody = new() { ["query"] = query };
        var queryResponse = await _httpClient!.PostAsync(
            "/query",
            new StringContent(queryBody.ToJsonString(), Encoding.UTF8, "application/json")
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        string queryResponseContent = await queryResponse.Content.ReadAsStringAsync();
        JsonDocument queryResponseJson = JsonDocument.Parse(queryResponseContent);
        var results = queryResponseJson.RootElement.GetProperty("value").EnumerateArray().ToList();
        Assert.NotEmpty(results);
        bool found = false;
        foreach (var result in results)
        {
            var twinId = result.GetProperty("$dtId").GetString();
            found = true;
        }
        Assert.True(found);
    }
}
