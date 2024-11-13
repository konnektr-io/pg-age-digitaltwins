using System.Text.Json;
using System.Text.Json.Nodes;
using AgeDigitalTwins.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class DigitalTwinsController : ControllerBase
{
    private const string _DEFAULT_GRAPH_NAME = "digitaltwins";
    private readonly AgeDigitalTwinsClient _client;

    public DigitalTwinsController(IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration));
        _client = new AgeDigitalTwinsClient(connectionString, new());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTwin(string id, CancellationToken cancellationToken)
    {
        var result = await _client.GetDigitalTwinAsync<JsonObject>(id, cancellationToken);
        return Ok(result);
        /* await using var client = CreateAgeClient();
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
        return NotFound(); */
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutTwin(string id, [FromBody] JsonObject twin, CancellationToken cancellationToken)
    {
        var result = await _client.CreateOrReplaceDigitalTwinAsync(id, twin, cancellationToken);
        return Ok(result);

        /* await using var client = CreateAgeClient();
        await client.OpenConnectionAsync(false, cancellationToken);
        // var propertiesJson = JsonSerializer.Serialize(twin);
        string cypher = $"CREATE (t:Twin {{dtId:'test',name:'test'}})";
        await client.ExecuteCypherAsync(
            _DEFAULT_GRAPH_NAME,
            cypher,
            cancellationToken);
        return CreatedAtAction(nameof(GetTwin), new { id = id }, twin); */
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

    /* [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTwin(string id, CancellationToken cancellationToken)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync(cancellationToken);
        await client.ExecuteCypherAsync(_DEFAULT_GRAPH_NAME,
            $"MATCH (t:Twin {{ $dtId: '{id}' }}) DELETE t",
            cancellationToken);
        return NoContent();
    } */
}