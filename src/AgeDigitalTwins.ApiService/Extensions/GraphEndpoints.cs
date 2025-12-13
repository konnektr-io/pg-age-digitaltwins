using AgeDigitalTwins.ServiceDefaults.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class GraphEndpoints
{
    public static WebApplication MapGraphEndpoints(this WebApplication app)
    {
        // Note: These endpoints are only exposed in development mode
        app.MapPut(
                "/graph/create",
                (
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    return client.CreateGraphAsync(cancellationToken);
                }
            );

        // This endpoint is only used for cleanup in tests
        app.MapDelete(
                "/graph/delete",
                (
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    return client.DropGraphAsync(cancellationToken);
                }
            );

        return app;
    }
}
