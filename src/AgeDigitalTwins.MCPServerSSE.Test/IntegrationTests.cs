using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

public class IntegrationTests
{
    private readonly HttpClient _client;

    public IntegrationTests()
    {
        var application = new WebApplicationFactory<Program>();
        _client = application.CreateClient();
    }

    [Fact]
    public async Task GetDigitalTwin_EndpointReturnsTwin()
    {
        // Arrange
        var twinId = "twin1";

        // Act
        var response = await _client.GetAsync($"/digitaltwins/{twinId}");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains(twinId, content);
    }
}
