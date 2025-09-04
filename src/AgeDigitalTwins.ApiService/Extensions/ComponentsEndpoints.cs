using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
using Json.Patch;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class ComponentsEndpoints
{
    public static WebApplication MapComponentsEndpoints(this WebApplication app)
    {
        // Group for Components endpoints with single component rate limiting
        var componentsGroup = app.MapGroup("/digitaltwins/{twinId}/components")
            .WithTags("Components")
            .RequireAuthorization();

        // GET Component - Light read operation
        componentsGroup
            .MapGet(
                "/{componentName}",
                (
                    string twinId,
                    string componentName,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    return client.GetComponentAsync<JsonDocument>(
                        twinId,
                        componentName,
                        cancellationToken
                    );
                }
            )
            .RequireRateLimiting("LightOperations")
            .WithName("GetComponent")
            .WithSummary("Retrieves a component from a digital twin by its name.");

        // PATCH Component - Heavy update operation
        componentsGroup
            .MapPatch(
                "/{componentName}",
                async (
                    string twinId,
                    string componentName,
                    JsonPatch patch,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = RequestHelper.ParseETag(httpContext, "If-Match");
                    await client.UpdateComponentAsync(
                        twinId,
                        componentName,
                        patch,
                        etag,
                        cancellationToken
                    );
                    return Results.NoContent();
                }
            )
            .RequireRateLimiting("HeavyOperations")
            .WithName("UpdateComponent")
            .WithSummary("Updates a component on a digital twin using a JSON patch.");

        return app;
    }
}
