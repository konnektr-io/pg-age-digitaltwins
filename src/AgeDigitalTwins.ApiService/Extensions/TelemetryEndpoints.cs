using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class TelemetryEndpoints
{
    public static WebApplication MapTelemetryEndpoints(this WebApplication app)
    {
        // Group for Telemetry endpoints with light rate limiting (telemetry is typically high volume)
        var telemetryGroup = app.MapGroup("/digitaltwins/{twinId}/telemetry")
            .WithTags("Telemetry")
            .RequireAuthorization();

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

                    await client.PublishTelemetryAsync(twinId, telemetryData, messageId, cancellationToken);
                    return Results.NoContent();
                }
            )
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
            .RequireRateLimiting("LightOperations")
            .WithName("PublishComponentTelemetry")
            .WithSummary("Publishes telemetry data for a specific component of a digital twin.");

        return app;
    }
}
