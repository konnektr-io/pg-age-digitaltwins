using System.Text.Json;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ApiService.Helpers;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using Json.Patch;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class ComponentsEndpoints
{
    public static WebApplication MapComponentsEndpoints(this WebApplication app)
    {
        // Group for Components endpoints
        var componentsGroup = app.MapGroup("/digitaltwins/{twinId}/components")
            .WithTags("Components");

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
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)
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
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("HeavyOperations")
            .WithName("UpdateComponent")
            .WithSummary("Updates a component on a digital twin using a JSON patch.");

        return app;
    }
}
