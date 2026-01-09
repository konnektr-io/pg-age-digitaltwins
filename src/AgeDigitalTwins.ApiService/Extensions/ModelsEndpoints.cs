using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
using AgeDigitalTwins.ApiService.Models;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
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
            .MapGet(
                "/{id}",
                [Authorize]
                async (
                    string id,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = httpContext.Request.Query;
                    bool includeBaseModelContents =
                        query.ContainsKey("includeBaseModelContents")
                        && bool.TryParse(query["includeBaseModelContents"], out var include)
                        && include;

                    var options = new GetModelOptions
                    {
                        IncludeBaseModelContents = includeBaseModelContents,
                    };

                    var model = await client.GetModelAsync(id, options, cancellationToken);
                    return Results.Json(model);
                }
            )
            .RequirePermission(ResourceType.Models, PermissionAction.Read)
            .RequireRateLimiting("AdminOperations")
            .WithName("GetModel")
            .WithSummary("Retrieves a specific model by its ID.");

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

        modelsGroup
            .MapPatch(
                "/{id}",
                [Authorize]
                async (
                    string id,
                    [FromBody] JsonElement[] patchOperations,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    // Parse the JSON Patch operations
                    // ADT only supports replacing the 'decommissioned' property
                    foreach (var operation in patchOperations)
                    {
                        var op = operation.GetProperty("op").GetString();
                        var path = operation.GetProperty("path").GetString();

                        if (op != "replace")
                        {
                            return Results.BadRequest(new { error = $"Only 'replace' operations are supported. Got: '{op}'" });
                        }

                        if (path != "/decommissioned")
                        {
                            return Results.BadRequest(new { error = $"Only the '/decommissioned' path can be patched. Got: '{path}'" });
                        }

                        var value = operation.GetProperty("value").GetBoolean();
                        await client.UpdateModelAsync(id, value, cancellationToken);
                    }

                    return Results.NoContent();
                }
            )
            .RequirePermission(ResourceType.Models, PermissionAction.Write)
            .RequireRateLimiting("AdminOperations")
            .WithName("UpdateModel")
            .WithSummary("Updates a model's decommissioned status (ADT-compatible PATCH).");

        modelsGroup
            .MapPut(
                "/{id}",
                [Authorize]
                async (
                    string id,
                    [FromBody] JsonElement model,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await client.ReplaceModelAsync(id, model.GetRawText(), cancellationToken);
                    return Results.Json(result);
                }
            )
            .RequirePermission(ResourceType.Models, PermissionAction.Write)
            .RequireRateLimiting("AdminOperations")
            .WithName("ReplaceModel")
            .WithSummary("Replaces a model definition (extended feature, validates against descendants).");

        modelsGroup
            .MapPost(
                "/search",
                [Authorize]
                async (
                    [FromBody] ModelSearchRequest request,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    var results = await client.SearchModelsAsync(
                        request.Query,
                        request.Vector,
                        request.Limit ?? 10,
                        cancellationToken
                    );
                    return Results.Json(results);
                }
            )
            .RequirePermission(ResourceType.Models, PermissionAction.Read)
            .RequireRateLimiting("AdminOperations")
            .WithName("SearchModels")
            .WithSummary("Searches models using lexical and/or vector similarity.");

        return app;
    }
}
