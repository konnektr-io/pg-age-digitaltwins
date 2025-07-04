using System.Text.Json;
using AgeDigitalTwins.ApiService.Models;
using Json.Patch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class RelationshipsEndpoints
{
    public static WebApplication MapRelationshipsEndpoints(this WebApplication app)
    {
        app.MapGet(
                "/digitaltwins/{id}/incomingrelationships",
                [Authorize]
                async (
                    string id,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? relationshipName = httpContext.Request.Query["relationshipName"];

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

                    // Get continuation token from query string
                    string? continuationToken = null;
                    if (
                        httpContext.Request.Query.TryGetValue(
                            "continuationToken",
                            out var continuationTokenStringValues
                        )
                    )
                    {
                        continuationToken = continuationTokenStringValues.ToString();
                    }

                    var page = await client
                        .GetIncomingRelationshipsAsync<JsonDocument>(id, cancellationToken)
                        .AsPages(continuationToken, maxItemsPerPage, cancellationToken)
                        .FirstAsync(cancellationToken);

                    return Results.Json(
                        new PageWithNextLink<JsonDocument?>(page, httpContext.Request)
                    );
                }
            )
            .WithName("ListIncomingRelationships")
            .WithTags("Relationships")
            .WithSummary("Lists all incoming relationships for a digital twin.");

        app.MapGet(
                "/digitaltwins/{id}/relationships",
                [Authorize]
                async (
                    string id,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? relationshipName = httpContext.Request.Query["relationshipName"];

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

                    // Get continuation token from query string
                    string? continuationToken = null;
                    if (
                        httpContext.Request.Query.TryGetValue(
                            "continuationToken",
                            out var continuationTokenStringValues
                        )
                    )
                    {
                        continuationToken = continuationTokenStringValues.ToString();
                    }

                    var page = await client
                        .GetRelationshipsAsync<JsonDocument>(
                            id,
                            relationshipName,
                            cancellationToken
                        )
                        .AsPages(continuationToken, maxItemsPerPage, cancellationToken)
                        .FirstAsync(cancellationToken);

                    return Results.Json(
                        new PageWithNextLink<JsonDocument?>(page, httpContext.Request)
                    );
                }
            )
            .WithName("ListRelationships")
            .WithTags("Relationships")
            .WithSummary("Lists all relationships for a digital twin.");

        app.MapGet(
                "/digitaltwins/{id}/relationships/{relationshipId}",
                [Authorize]
                (
                    string id,
                    string relationshipId,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    return client.GetRelationshipAsync<JsonDocument>(
                        id,
                        relationshipId,
                        cancellationToken
                    );
                }
            )
            .WithName("GetRelationship")
            .WithTags("Relationships")
            .WithSummary("Retrieves a specific relationship by its ID.");

        app.MapPut(
                "/digitaltwins/{id}/relationships/{relationshipId}",
                [Authorize]
                (
                    string id,
                    string relationshipId,
                    JsonDocument relationship,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = null;
                    if (
                        httpContext.Request.Headers.TryGetValue(
                            "If-None-Match",
                            out StringValues etagValues
                        )
                        && etagValues.Count > 0
                    )
                    {
                        etag = etagValues[0];
                    }
                    return client.CreateOrReplaceRelationshipAsync(
                        id,
                        relationshipId,
                        relationship,
                        etag,
                        cancellationToken
                    );
                }
            )
            .WithName("CreateOrReplaceRelationship")
            .WithTags("Relationships")
            .WithSummary("Creates or replaces a relationship for a digital twin.");

        app.MapPatch(
                "/digitaltwins/{id}/relationships/{relationshipId}",
                [Authorize]
                async (
                    string id,
                    string relationshipId,
                    JsonPatch patch,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = null;
                    if (
                        httpContext.Request.Headers.TryGetValue(
                            "If-Match",
                            out StringValues etagValues
                        )
                        && etagValues.Count > 0
                    )
                    {
                        etag = etagValues[0];
                    }
                    await client.UpdateRelationshipAsync(
                        id,
                        relationshipId,
                        patch,
                        etag,
                        cancellationToken
                    );
                    return Results.NoContent();
                }
            )
            .WithName("UpdateRelationship")
            .WithTags("Relationships")
            .WithSummary("Updates a specific relationship for a digital twin.");

        app.MapDelete(
                "/digitaltwins/{id}/relationships/{relationshipId}",
                [Authorize]
                async (
                    string id,
                    string relationshipId,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    await client.DeleteRelationshipAsync(id, relationshipId, cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteRelationship")
            .WithTags("Relationships")
            .WithSummary("Deletes a specific relationship for a digital twin.");

        return app;
    }
}
