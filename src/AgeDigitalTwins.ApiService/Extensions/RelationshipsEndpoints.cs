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

        // GET Incoming Relationships - Light read operation
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
            .RequireRateLimiting("LightOperations")
            .WithName("ListIncomingRelationships")
            .WithSummary("Lists all incoming relationships for a digital twin.");

        // GET Relationships - Light read operation
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
            .RequireRateLimiting("LightOperations")
            .WithName("ListRelationships")
            .WithSummary("Lists all relationships for a digital twin.");

        // GET Single Relationship - Light read operation
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
            .RequireRateLimiting("LightOperations")
            .WithName("GetRelationship")
            .WithSummary("Retrieves a specific relationship by its ID.");

        // PUT Relationship - Heavy create/replace operation
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
            .RequireRateLimiting("HeavyOperations")
            .WithName("CreateOrReplaceRelationship")
            .WithSummary("Creates or replaces a relationship for a digital twin.");

        // PATCH Relationship - Heavy update operation
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
            .RequireRateLimiting("HeavyOperations")
            .WithName("UpdateRelationship")
            .WithSummary("Updates a specific relationship for a digital twin.");

        // DELETE Relationship - Heavy delete operation
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
            .RequireRateLimiting("HeavyOperations")
            .WithName("DeleteRelationship")
            .WithSummary("Deletes a specific relationship for a digital twin.");

        return app;
    }
}
