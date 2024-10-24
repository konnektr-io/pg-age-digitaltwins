using AgeDigitalTwins.Api.Models;
using ApacheAGE;
using ApacheAGE.Types;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AgeDigitalTwins.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class DigitalTwinsController(IConfiguration configuration) : ControllerBase
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration));
    private AgeClientBuilder CreateAgeClientBuilder() => new(_connectionString);
    private AgeClient CreateAgeClient() => CreateAgeClientBuilder().Build();
    private const string _DEFAULT_GRAPH_NAME = "digitaltwins";

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTwin(string id, CancellationToken cancellationToken)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync(cancellationToken);
        string cypher = $"MATCH (t:Twin {{ $dtId: '{id}' }}) RETURN t";
        await using var dataReader = await client.ExecuteQueryAsync(
            $"SELECT * FROM cypher('{_DEFAULT_GRAPH_NAME}', $$ {cypher} $$) as (t agtype);",
            cancellationToken);
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
    public async Task<IActionResult> PutTwin(string id, [FromBody] string twin, CancellationToken cancellationToken)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync(false, cancellationToken);
        // var propertiesJson = JsonSerializer.Serialize(twin);
        string cypher = $"CREATE (t:Twin {{dtId:'test',name:'test'}})";
        await client.ExecuteCypherAsync(
            _DEFAULT_GRAPH_NAME,
            cypher,
            cancellationToken);
        return CreatedAtAction(nameof(GetTwin), new { id = id }, twin);
    }

    /* 
        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchTwin(string id, [FromBody] Twin twin, CancellationToken cancellationToken)
        {
            await using var client = CreateAgeClient();
            await client.OpenConnectionAsync();
            var propertiesJson = JsonSerializer.Serialize(twin.Properties);
            await client.ExecuteCypherAsync(
                $"MATCH (t:Twin {{ $dtId: '{id}' }}) SET t.properties = '{propertiesJson}'",
                cancellationToken);
            return NoContent();
        }
     */

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTwin(string id, CancellationToken cancellationToken)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync(cancellationToken);
        await client.ExecuteCypherAsync(_DEFAULT_GRAPH_NAME,
            $"MATCH (t:Twin {{ $dtId: '{id}' }}) DELETE t",
            cancellationToken);
        return NoContent();
    }
}