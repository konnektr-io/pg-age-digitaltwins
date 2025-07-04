using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class GraphEndpoints
{
    public static WebApplication MapGraphEndpoints(this WebApplication app)
    {
        // When the client is initiated, a new graph will automatically be created if the specified graph doesn't exist
        // Creating and dropping graphs should be done in the control plane
        if (app.Environment.IsDevelopment())
        {
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
        }

        return app;
    }
}
