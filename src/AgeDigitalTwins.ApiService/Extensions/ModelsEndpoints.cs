using System.Text.Json;
using AgeDigitalTwins.ApiService.Authorization;
using AgeDigitalTwins.ApiService.Authorization.Models;
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
        var modelsGroup = app.MapGroup("/models").WithTags("Models");

        modelsGroup
            .MapGet(
                "/",
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
            .RequirePermission(ResourceType.Models, PermissionAction.Read)
            .RequireRateLimiting("AdminOperations")
            .WithName("ListModels")
            .WithSummary("Lists all models in the digital twins graph.");

        modelsGroup
            .MapPost(
                "/",
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
            .RequirePermission(ResourceType.Models, PermissionAction.Write)
            .RequireRateLimiting("AdminOperations")
            .WithName("CreateModels")
            .WithSummary("Creates new models in the digital twins graph.");

        modelsGroup
            .MapDelete(
                "/",
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
            .RequirePermission(ResourceType.Models, PermissionAction.Delete)
            .RequireRateLimiting("AdminOperations")
            .WithName("DeleteAllModels")
            .WithSummary("Deletes all models in the digital twins graph.");

        modelsGroup
            .MapDelete(
                "/{id}",
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
            .RequirePermission(ResourceType.Models, PermissionAction.Delete)
            .RequireRateLimiting("AdminOperations")
            .WithName("DeleteModel")
            .WithSummary("Deletes a specific model by its ID.");

        return app;
    }
}
