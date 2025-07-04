using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
using AgeDigitalTwins.ApiService.Models;
using AgeDigitalTwins.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class ModelsEndpoints
{
    public static WebApplication MapModelsEndpoints(this WebApplication app)
    {
        app.MapGet(
                "/models",
                [Authorize]
                async (
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = httpContext.Request.Query;

                    // Parse query parameters
                    string[] dependenciesFor =
                    [
                        .. query["dependenciesFor"].Where<string>(x => !string.IsNullOrEmpty(x)),
                    ];
                    bool includeModelDefinition =
                        query.ContainsKey("includeModelDefinition")
                        && bool.TryParse(query["includeModelDefinition"], out var include)
                        && include;

                    var options = new GetModelsOptions
                    {
                        DependenciesFor = dependenciesFor.Length > 0 ? dependenciesFor : null,
                        IncludeModelDefinition = includeModelDefinition,
                    };

                    var (maxItemsPerPage, continuationToken) = RequestHelper.ParsePagination(
                        httpContext
                    );

                    var page = await client
                        .GetModelsAsync(options, cancellationToken)
                        .AsPages(continuationToken, maxItemsPerPage, cancellationToken)
                        .FirstAsync(cancellationToken);

                    return Results.Json(
                        new PageWithNextLink<DigitalTwinsModelData?>(page, httpContext.Request)
                    );
                }
            )
            .WithName("ListModels")
            .WithTags("Models")
            .WithSummary("Lists all models in the digital twins graph.");

        app.MapPost(
                "/models",
                [Authorize]
                (
                    JsonElement[] models,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    return client.CreateModelsAsync(
                        models.Select(m => m.GetRawText()),
                        cancellationToken
                    );
                }
            )
            .WithName("CreateModels")
            .WithTags("Models")
            .WithSummary("Creates new models in the digital twins graph.");

        app.MapDelete(
                "/models",
                [Authorize]
                async (
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    await client.DeleteAllModelsAsync(cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteAllModels")
            .WithTags("Models")
            .WithSummary("Deletes all models in the digital twins graph.");

        app.MapDelete(
                "/models/{id}",
                [Authorize]
                async (
                    string id,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    await client.DeleteModelAsync(id, cancellationToken);
                    return Results.NoContent();
                }
            )
            .WithName("DeleteModel")
            .WithTags("Models")
            .WithSummary("Deletes a specific model by its ID.");

        return app;
    }
}
