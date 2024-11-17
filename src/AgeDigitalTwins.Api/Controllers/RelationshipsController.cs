using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using AgeDigitalTwins.Api.Models;

namespace AgeDigitalTwins.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class RelationshipsController(IConfiguration configuration) : ControllerBase
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration));
    private AgeClient CreateAgeClient() => CreateAgeClientBuilder().Build();
    private const string _DEFAULT_GRAPH_NAME = "digitaltwins";

    [HttpGet("{id}/relationships/{relationshipId}")]
    public async Task<IActionResult> GetRelationship(string id, string relationshipId, CancellationToken cancellationToken)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync(cancellationToken);
        string cypher = $"MATCH (a:Twin {{ $dtId: '{id}' }})-[r:RELATIONSHIP {{ $dtId: '{relationshipId}' }}]->(b) RETURN r";
        await using var dataReader = await client.ExecuteQueryAsync(
            $"SELECT * FROM cypher('{_DEFAULT_GRAPH_NAME}', $$ {cypher} $$) as (r agtype);",
            cancellationToken);
        if (await dataReader.ReadAsync())
        {
            var agResult = dataReader.GetValue<Agtype?>(0);
            var relationship = agResult?.GetEdge();
            var properties = relationship?.Properties;
            var json = JsonSerializer.Serialize(properties);
            return Ok(json);
        }
        return NotFound();
    }

    [HttpPut("{id}/relationships/{relationshipId}")]
    public async Task<IActionResult> PutRelationship(string id, string relationshipId, [FromBody] Relationship relationship)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        var propertiesJson = JsonSerializer.Serialize(relationship.Properties);
        await client.ExecuteQueryAsync(
            $"MATCH (a:Twin {{ $dtId: '{id}' }}), (b:Twin {{ $dtId: '{relationship.TargetId}' }}) " +
            $"CREATE (a)-[r:RELATIONSHIP {{ $dtId: '{relationshipId}', properties: '{propertiesJson}' }}]->(b)");
        return CreatedAtAction(nameof(PutRelationship), new { id = id }, relationship);
    }

    /* 
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
        } */
}