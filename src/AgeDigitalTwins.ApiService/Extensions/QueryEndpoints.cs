using System.Text.Json;
using AgeDigitalTwins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class QueryEndpoints
{
    public static WebApplication MapQueryEndpoints(this WebApplication app)
    {
        app.MapPost(
                "/query",
                [Authorize]
                async (
                    HttpContext httpContext,
                    JsonElement requestBody,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    int? maxItemsPerPage = 2000; // Default value

                    // Parse max-items-per-page header
                    if (
                        httpContext.Request.Headers.TryGetValue(
                            "max-items-per-page",
                            out var maxItemsHeader
                        )
                    )
                    {
                        if (int.TryParse(maxItemsHeader, out var maxItems))
                        {
                            maxItemsPerPage = maxItems;
                        }
                    }

                    // Get continuation token from request body
                    string? continuationToken = null;
                    if (
                        requestBody.TryGetProperty(
                            "continuationToken",
                            out JsonElement continuationTokenElement
                        )
                        && continuationTokenElement.ValueKind == JsonValueKind.String
                    )
                    {
                        continuationToken = continuationTokenElement.GetString();
                    }

                    string? query = null;
                    if (
                        requestBody.TryGetProperty("query", out JsonElement queryElement)
                        && queryElement.ValueKind == JsonValueKind.String
                    )
                    {
                        query = queryElement.GetString();
                    }

                    if (string.IsNullOrEmpty(continuationToken) && string.IsNullOrEmpty(query))
                    {
                        return Results.BadRequest(
                            new
                            {
                                error = "Invalid request body. Expected a JSON object with a 'query' property.",
                            }
                        );
                    }

                    var page = await client
                        .QueryAsync<JsonDocument>(query, cancellationToken)
                        .AsPages(continuationToken, maxItemsPerPage, cancellationToken)
                        .FirstAsync(cancellationToken);
                    return Results.Json(page);
                }
            )
            .WithName("Query")
            .WithTags("Query")
            .WithSummary("Executes a query against the digital twins graph with pagination.");

        return app;
    }
}
