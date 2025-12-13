using System.Text.Json;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class TelemetryEndpoints
{
    public static WebApplication MapTelemetryEndpoints(this WebApplication app)
    {
        // Group for Telemetry endpoints
        var telemetryGroup = app.MapGroup("/digitaltwins/{twinId}/telemetry").WithTags("Telemetry");

        // POST Telemetry - Light operation as it's just a pass-through
        telemetryGroup
            .MapPost(
                "",
                async (
                    string twinId,
                    JsonDocument telemetryData,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    // Extract optional message ID from headers
                    string? messageId = httpContext.Request.Headers["Message-Id"].FirstOrDefault();

                    await client.PublishTelemetryAsync(
                        twinId,
                        telemetryData,
                        messageId,
                        cancellationToken
                    );
                    return Results.NoContent();
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("LightOperations")
            .WithName("PublishTelemetry")
            .WithSummary("Publishes telemetry data for a digital twin.");

        // POST Component Telemetry - Light operation as it's just a pass-through
        telemetryGroup
            .MapPost(
                "/components/{componentName}",
                async (
                    string twinId,
                    string componentName,
                    JsonDocument telemetryData,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    // Extract optional message ID from headers
                    string? messageId = httpContext.Request.Headers["Message-Id"].FirstOrDefault();

                    await client.PublishComponentTelemetryAsync(
                        twinId,
                        componentName,
                        telemetryData,
                        messageId,
                        cancellationToken
                    );
                    return Results.NoContent();
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("LightOperations")
            .WithName("PublishComponentTelemetry")
            .WithSummary("Publishes telemetry data for a specific component of a digital twin.");

        return app;
    }
}
