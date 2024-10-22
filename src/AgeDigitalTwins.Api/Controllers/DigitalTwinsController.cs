using ApacheAge;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AgeDigitalTwins.Api.Models;

[ApiController]
[Route("[controller]")]
public class DigitalTwinsController : ControllerBase
{
    private readonly string _connectionString;

    public DigitalTwinsController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    private AgeClientBuilder CreateAgeClientBuilder() => new(_connectionString);
    private AgeClient CreateAgeClient() => CreateAgeClientBuilder().Build();

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTwin(string id)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        await using var dataReader = await client.ExecuteCypherAsync(
            $"MATCH (t:Twin {{ $dtId: '{id}' }}) RETURN t");
        if (await dataReader.ReadAsync())
        {
            var agResult = dataReader.GetValue<Agtype?>(0);
            var vertex = agResult?.GetVertex();
            var properties = vertex?.Properties;
            var json = JsonSerializer.Serialize(properties);
            return Ok(json);
        }
        return NotFound();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutTwin(string id, [FromBody] Twin twin)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        var propertiesJson = JsonSerializer.Serialize(twin.Properties);
        await client.ExecuteCypherAsync(
            $"CREATE (t:Twin {{ $dtId: '{id}', properties: '{propertiesJson}' }})");
        return CreatedAtAction(nameof(GetTwin), new { id = id }, twin);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> PatchTwin(string id, [FromBody] Twin twin)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        var propertiesJson = JsonSerializer.Serialize(twin.Properties);
        await client.ExecuteCypherAsync(
            $"MATCH (t:Twin {{ $dtId: '{id}' }}) SET t.properties = '{propertiesJson}'");
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTwin(string id)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        await client.ExecuteCypherAsync(
            $"MATCH (t:Twin {{ $dtId: '{id}' }}) DELETE t");
        return NoContent();
    }
}