using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class QueryEndpoints
{
    /// <summary>
    /// A constant that is used to as the query-charge header field in the query page response.
    /// </summary>
    private const string QueryChargeHeader = "query-charge";

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

                    // Set X-Query-Charge header
                    if (page.QueryCharge.HasValue)
                    {
                        httpContext.Response.Headers[QueryChargeHeader] =
                            page.QueryCharge.Value.ToString();
                    }

                    return Results.Json(page);
                }
            )
            .RequireRateLimiting("MediumOperations")
            .WithName("Query")
            .WithTags("Query")
            .WithSummary("Executes a query against the digital twins graph with pagination.");

        return app;
    }
}
