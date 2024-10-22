using ApacheAge;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AgeDigitalTwins.Api.Models;

[ApiController]
[Route("[controller]")]
public class DigitalTwinsController : ControllerBase
{
    [HttpPost("query")]
    public async Task<IActionResult> ExecuteQuery([FromBody] string cypherQuery)
    {
        await using var client = CreateAgeClient();
        await client.OpenConnectionAsync();
        await using var dataReader = await client.ExecuteCypherAsync(cypherQuery);
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