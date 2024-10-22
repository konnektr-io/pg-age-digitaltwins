using Microsoft.AspNetCore.Mvc;
using ApacheAge;
using System.Text.Json;

namespace AgeDigitalTwins.Api.Models;

[ApiController]
[Route("[controller]")]
public class RelationshipsController : ControllerBase
{
    private readonly string _connectionString;

    public RelationshipsController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    private AgeClientBuilder CreateAgeClientBuilder() => new(_connectionString);
    private AgeClient CreateAgeClient() => CreateAgeClientBuilder().Build();

    [HttpPut("{id}/relationships/{relationshipId}")]
    public async Task<IActionResult> PutRelationship(string id, string relationshipId, [FromBody] Relationship relationship)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        var propertiesJson = JsonSerializer.Serialize(relationship.Properties);
        await client.ExecuteCypherAsync(
            $"MATCH (a:Twin {{ $dtId: '{id}' }}), (b:Twin {{ $dtId: '{relationship.TargetId}' }}) " +
            $"CREATE (a)-[r:RELATIONSHIP {{ $dtId: '{relationshipId}', properties: '{propertiesJson}' }}]->(b)");
        return CreatedAtAction(nameof(GetRelationships), new { id = id }, relationship);
    }

    [HttpDelete("{id}/relationships/{relationshipId}")]
    public async Task<IActionResult> DeleteRelationship(string id, string relationshipId)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        await client.ExecuteCypherAsync(
            $"MATCH (a:Twin {{ $dtId: '{id}' }})-[r:RELATIONSHIP {{ $dtId: '{relationshipId}' }}]->() DELETE r");
        return NoContent();
    }

    [HttpGet("{id}/relationships")]
    public async Task<IActionResult> GetRelationships(string id)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        await using var dataReader = await client.ExecuteCypherAsync(
            $"MATCH (a:Twin {{ $dtId: '{id}' }})-[r:RELATIONSHIP]->(b) RETURN r");
        var relationships = new List<string>();
        while (await dataReader.ReadAsync())
        {
            var agResult = dataReader.GetValue<Agtype?>(0);
            var relationship = agResult?.GetEdge();
            var properties = relationship?.Properties;
            var json = JsonSerializer.Serialize(properties);
            relationships.Add(json);
        }
        return Ok(relationships);
    }

    [HttpGet("{id}/incomingrelationships")]
    public async Task<IActionResult> GetIncomingRelationships(string id)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        await using var dataReader = await client.ExecuteCypherAsync(
            $"MATCH (a)-[r:RELATIONSHIP]->(b:Twin {{ $dtId: '{id}' }}) RETURN r");
        var relationships = new List<string>();
        while (await dataReader.ReadAsync())
        {
            var agResult = dataReader.GetValue<Agtype?>(0);
            var relationship = agResult?.GetEdge();
            var properties = relationship?.Properties;
            var json = JsonSerializer.Serialize(properties);
            relationships.Add(json);
        }
        return Ok(relationships);
    }
}