using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
using AgeDigitalTwins.ApiService.Models;
using Json.Patch;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class RelationshipsEndpoints
{
    public static WebApplication MapRelationshipsEndpoints(this WebApplication app)
    {
        // Group for relationship endpoints - these are part of Digital Twins API
        var relationshipsGroup = app.MapGroup("/digitaltwins")
            .WithTags("Relationships")
            .RequireAuthorization();

        // GET Incoming Relationships - Read operations (1,000 requests per second)
        relationshipsGroup
            .MapGet(
                "/{id}/incomingrelationships",
                async (
                    string id,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? relationshipName = httpContext.Request.Query["relationshipName"];

                    var (maxItemsPerPage, continuationToken) = RequestHelper.ParsePagination(
                        httpContext
                    );

                    var page = await client
                        .GetIncomingRelationshipsAsync<JsonDocument>(id, cancellationToken)
                        .AsPages(continuationToken, maxItemsPerPage, cancellationToken)
                        .FirstAsync(cancellationToken);

                    return Results.Json(
                        new PageWithNextLink<JsonDocument?>(page, httpContext.Request)
                    );
                }
            )
            .RequireRateLimiting("DigitalTwinsApiRead")
            .RequireRateLimiting("DigitalTwinsApiSingleTwin")
            .WithName("ListIncomingRelationships")
            .WithSummary("Lists all incoming relationships for a digital twin.");

        // GET Relationships - Read operations (1,000 requests per second)
        relationshipsGroup
            .MapGet(
                "/{id}/relationships",
                async (
                    string id,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? relationshipName = httpContext.Request.Query["relationshipName"];

                    var (maxItemsPerPage, continuationToken) = RequestHelper.ParsePagination(
                        httpContext
                    );

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
            .RequireRateLimiting("DigitalTwinsApiRead")
            .RequireRateLimiting("DigitalTwinsApiSingleTwin")
            .WithName("ListRelationships")
            .WithSummary("Lists all relationships for a digital twin.");

        // GET Single Relationship - Read operations (1,000 requests per second)
        relationshipsGroup
            .MapGet(
                "/{id}/relationships/{relationshipId}",
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
            .RequireRateLimiting("DigitalTwinsApiRead")
            .RequireRateLimiting("DigitalTwinsApiSingleTwin")
            .WithName("GetRelationship")
            .WithSummary("Retrieves a specific relationship by its ID.");

        // PUT Relationship - Create/Replace operations (500 create/delete per second)
        relationshipsGroup
            .MapPut(
                "/{id}/relationships/{relationshipId}",
                (
                    string id,
                    string relationshipId,
                    JsonDocument relationship,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = RequestHelper.ParseETag(httpContext, "If-None-Match");
                    return client.CreateOrReplaceRelationshipAsync(
                        id,
                        relationshipId,
                        relationship,
                        etag,
                        cancellationToken
                    );
                }
            )
            .RequireRateLimiting("DigitalTwinsApiCreateDelete")
            .RequireRateLimiting("DigitalTwinsApiSingleTwin")
            .WithName("CreateOrReplaceRelationship")
            .WithSummary("Creates or replaces a relationship for a digital twin.");

        // PATCH Relationship - Write operations (1,000 patch requests per second)
        relationshipsGroup
            .MapPatch(
                "/{id}/relationships/{relationshipId}",
                async (
                    string id,
                    string relationshipId,
                    JsonPatch patch,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = RequestHelper.ParseETag(httpContext, "If-Match");
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
            .RequireRateLimiting("DigitalTwinsApiWrite")
            .RequireRateLimiting("DigitalTwinsApiSingleTwin")
            .WithName("UpdateRelationship")
            .WithSummary("Updates a specific relationship for a digital twin.");

        // DELETE Relationship - Create/Delete operations (500 create/delete per second)
        relationshipsGroup
            .MapDelete(
                "/{id}/relationships/{relationshipId}",
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
            .RequireRateLimiting("DigitalTwinsApiCreateDelete")
            .RequireRateLimiting("DigitalTwinsApiSingleTwin")
            .WithName("DeleteRelationship")
            .WithSummary("Deletes a specific relationship for a digital twin.");

        return app;
    }
}
