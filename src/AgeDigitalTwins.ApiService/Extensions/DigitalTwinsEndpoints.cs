using AgeDigitalTwins.ApiService.Helpers;
using AgeDigitalTwins.ApiService.Models;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using Json.Patch;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class DigitalTwinsEndpoints
{
    public static WebApplication MapDigitalTwinsEndpoints(this WebApplication app)
    {
        // Group for Digital Twins endpoints
        var digitalTwinsGroup = app.MapGroup("/digitaltwins").WithTags("Digital Twins");

        // GET Digital Twin - Light read operation
        digitalTwinsGroup
            .MapGet(
                "/{id}",
                (
                    string id,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    return client.GetDigitalTwinAsync<BasicDigitalTwin>(id, cancellationToken);
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)
            .RequireRateLimiting("LightOperations")
            .WithName("GetDigitalTwin")
            .WithSummary("Retrieves a digital twin by its ID.")
            .Produces<BasicDigitalTwin>();

        // PUT Digital Twin - Heavy create/replace operation
        digitalTwinsGroup
            .MapPut(
                "/{id}",
                (
                    string id,
                    BasicDigitalTwin digitalTwin,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = RequestHelper.ParseETag(httpContext, "If-None-Match");
                    string? userId = RequestHelper.ParseUserId(httpContext);
                    return client.CreateOrReplaceDigitalTwinAsync(
                        id,
                        digitalTwin,
                        etag,
                        userId,
                        cancellationToken
                    );
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("HeavyOperations")
            .WithName("CreateOrReplaceDigitalTwin")
            .WithSummary("Creates or replaces a digital twin by its ID.")
            .Produces<BasicDigitalTwin>();

        // PATCH Digital Twin - Heavy update operation
        digitalTwinsGroup
            .MapPatch(
                "/{id}",
                async (
                    string id,
                    JsonPatch patch,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = RequestHelper.ParseETag(httpContext, "If-Match");
                    string? userId = RequestHelper.ParseUserId(httpContext);
                    await client.UpdateDigitalTwinAsync(id, patch, etag, userId, cancellationToken);
                    return Results.NoContent();
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("HeavyOperations")
            .WithName("UpdateDigitalTwin")
            .WithSummary("Updates a digital twin by its ID.");

        // DELETE Digital Twin - Heavy delete operation
        digitalTwinsGroup
            .MapDelete(
                "/{id}",
                async (
                    string id,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    await client.DeleteDigitalTwinAsync(id, cancellationToken);
                    return Results.NoContent();
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Delete)
            .RequireRateLimiting("HeavyOperations")
            .WithName("DeleteDigitalTwin")
            .WithSummary("Deletes a digital twin by its ID.");

        // POST /digitaltwins - Batch create/replace digital twins
        digitalTwinsGroup
            .MapPost(
                "/",
                async (
                    [FromBody] IEnumerable<BasicDigitalTwin> digitalTwins,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await client.CreateOrReplaceDigitalTwinsAsync(
                        digitalTwins,
                        cancellationToken
                    );
                    return Results.Ok(result);
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("HeavyOperations")
            .WithName("CreateOrReplaceDigitalTwinsBatch")
            .WithSummary("Creates or replaces multiple digital twins in a single batch operation.")
            .Produces<BatchDigitalTwinResult>();

        // POST /digitaltwins/search - Hybrid search endpoint
        digitalTwinsGroup
            .MapPost(
                "/search",
                async (
                    [FromBody] DigitalTwinSearchRequest request,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await client.HybridSearchAsync(
                        request.Vector,
                        request.EmbeddingProperty ?? "embedding",
                        request.ModelFilter,
                        request.Limit ?? 10,
                        cancellationToken
                    );
                    return Results.Content(result, "application/json");
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)
            .RequireRateLimiting("LightOperations")
            .WithName("SearchDigitalTwins")
            .WithSummary(
                "Performs a hybrid search on digital twins using vector similarity and metadata filter."
            );

        return app;
    }
}
