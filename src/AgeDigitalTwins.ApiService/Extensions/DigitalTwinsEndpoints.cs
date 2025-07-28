using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
using Json.Patch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class DigitalTwinsEndpoints
{
    public static WebApplication MapDigitalTwinsEndpoints(this WebApplication app)
    {
        app.MapGet(
                "/digitaltwins/{id}",
                [Authorize]
                (
                    string id,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    return client.GetDigitalTwinAsync<JsonDocument>(id, cancellationToken);
                }
            )
            .WithName("GetDigitalTwin")
            .WithTags("Digital Twins")
            .WithSummary("Retrieves a digital twin by its ID.");

        app.MapPut(
                "/digitaltwins/{id}",
                [Authorize]
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
            .WithName("CreateOrReplaceDigitalTwin")
            .WithTags("Digital Twins")
            .WithSummary("Creates or replaces a digital twin by its ID.");

        app.MapPatch(
                "/digitaltwins/{id}",
                [Authorize]
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
            .WithName("UpdateDigitalTwin")
            .WithTags("Digital Twins")
            .WithSummary("Updates a digital twin by its ID.");

        app.MapDelete(
                "/digitaltwins/{id}",
                [Authorize]
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
            .WithName("DeleteDigitalTwin")
            .WithTags("Digital Twins")
            .WithSummary("Deletes a digital twin by its ID.");

        return app;
    }
}
