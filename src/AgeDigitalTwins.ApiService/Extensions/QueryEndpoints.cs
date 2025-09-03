using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
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
                    var (maxItemsPerPage, continuationToken) =
                        RequestHelper.ParsePaginationFromBody(httpContext, requestBody);

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
                                error = "Invalid request body. Expected a JSON object with at least one of 'query' or 'continuationToken' properties.",
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
            .RequireRateLimiting("QueryApi")
            .WithName("Query")
            .WithTags("Query")
            .WithSummary("Executes a query against the digital twins graph with pagination.");

        return app;
    }
}
