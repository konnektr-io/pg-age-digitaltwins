using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
using Json.Patch;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class DigitalTwinsEndpoints
{
    public static WebApplication MapDigitalTwinsEndpoints(this WebApplication app)
    {
        // Group for Digital Twins endpoints with single twin rate limiting
        var digitalTwinsGroup = app.MapGroup("/digitaltwins")
            .WithTags("Digital Twins")
            .RequireAuthorization();

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
                    return client.GetDigitalTwinAsync<JsonDocument>(id, cancellationToken);
                }
            )
            .RequireRateLimiting("LightOperations")
            .WithName("GetDigitalTwin")
            .WithSummary("Retrieves a digital twin by its ID.");

        // PUT Digital Twin - Heavy create/replace operation
        digitalTwinsGroup
            .MapPut(
                "/{id}",
                (
                    string id,
                    JsonDocument digitalTwin,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = RequestHelper.ParseETag(httpContext, "If-None-Match");
                    return client.CreateOrReplaceDigitalTwinAsync(
                        id,
                        digitalTwin,
                        etag,
                        cancellationToken
                    );
                }
            )
            .RequireRateLimiting("HeavyOperations")
            .WithName("CreateOrReplaceDigitalTwin")
            .WithSummary("Creates or replaces a digital twin by its ID.");

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
                    await client.UpdateDigitalTwinAsync(id, patch, etag, cancellationToken);
                    return Results.NoContent();
                }
            )
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
            .RequireRateLimiting("HeavyOperations")
            .WithName("DeleteDigitalTwin")
            .WithSummary("Deletes a digital twin by its ID.");

        return app;
    }
}
