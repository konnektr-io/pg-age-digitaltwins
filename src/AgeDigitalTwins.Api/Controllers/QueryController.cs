using ApacheAGE;
using ApacheAGE.Types;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AgeDigitalTwins.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class QueryController(IConfiguration configuration) : ControllerBase
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration));
    private AgeClientBuilder CreateAgeClientBuilder() => new(_connectionString);
    private AgeClient CreateAgeClient() => CreateAgeClientBuilder().Build();
    private const string _DEFAULT_GRAPH_NAME = "digitaltwins";

    [HttpPost("query")]
    public async Task<IActionResult> ExecuteQuery([FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("query", out var queryElement) || queryElement.ValueKind != JsonValueKind.String)
        {
            return BadRequest("Invalid request body. Expected a JSON object with a 'query' property.");
        }

        var query = queryElement.GetString();
        if (string.IsNullOrEmpty(query))
        {
            return BadRequest("Invalid request body. The 'query' property must not be empty.");
        }

        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        await using var dataReader = await client.ExecuteQueryAsync(query);
        var results = new List<string>();
        while (await dataReader.ReadAsync())
        {
            var agResult = dataReader.GetValue<Agtype?>(0);
            var json = JsonSerializer.Serialize(agResult);
            results.Add(json);
        }
        return Ok(results);
    }
}