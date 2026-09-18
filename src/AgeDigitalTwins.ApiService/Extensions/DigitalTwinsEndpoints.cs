using System.Text.Json;
using AgeDigitalTwins.ApiService.Helpers;
using AgeDigitalTwins.ApiService.Models;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using Json.Patch;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService.Extensions;

public static class DigitalTwinsEndpoints
{
    public static WebApplication MapDigitalTwinsEndpoints(this WebApplication app)
    {
        // Group for Digital Twins endpoints
        var digitalTwinsGroup = app.MapGroup("/digitaltwins").WithTags("Digital Twins");

        // GET Digital Twin - Light read operation
        digitalTwinsGroup
            .MapGet(
                "/{id}",
                (
                    string id,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    return client.GetDigitalTwinAsync<BasicDigitalTwin>(id, cancellationToken);
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)
            .RequireRateLimiting("LightOperations")
            .WithName("GetDigitalTwin")
            .WithSummary("Retrieves a digital twin by its ID.")
            .Produces<BasicDigitalTwin>();

        // PUT Digital Twin - Heavy create/replace operation
        digitalTwinsGroup
            .MapPut(
                "/{id}",
                (
                    string id,
                    BasicDigitalTwin digitalTwin,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = RequestHelper.ParseETag(httpContext, "If-None-Match");
                    string? userId = RequestHelper.ParseUserId(httpContext);
                    return client.CreateOrReplaceDigitalTwinAsync(
                        id,
                        digitalTwin,
                        etag,
                        userId,
                        cancellationToken
                    );
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("HeavyOperations")
            .WithName("CreateOrReplaceDigitalTwin")
            .WithSummary("Creates or replaces a digital twin by its ID.")
            .Produces<BasicDigitalTwin>();

        // PATCH Digital Twin - Heavy update operation
        digitalTwinsGroup
            .MapPatch(
                "/{id}",
                async (
                    string id,
                    JsonPatch patch,
                    HttpContext httpContext,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    string? etag = RequestHelper.ParseETag(httpContext, "If-Match");
                    string? userId = RequestHelper.ParseUserId(httpContext);
                    await client.UpdateDigitalTwinAsync(id, patch, etag, userId, cancellationToken);
                    return Results.NoContent();
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("HeavyOperations")
            .WithName("UpdateDigitalTwin")
            .WithSummary("Updates a digital twin by its ID.");

        // DELETE Digital Twin - Heavy delete operation
        digitalTwinsGroup
            .MapDelete(
                "/{id}",
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
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Delete)
            .RequireRateLimiting("HeavyOperations")
            .WithName("DeleteDigitalTwin")
            .WithSummary("Deletes a digital twin by its ID.");

        // POST /digitaltwins - Batch create/replace digital twins
        digitalTwinsGroup
            .MapPost(
                "/",
                async (
                    [FromBody] IEnumerable<BasicDigitalTwin> digitalTwins,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await client.CreateOrReplaceDigitalTwinsAsync(
                        digitalTwins,
                        userId: null,
                        cancellationToken
                    );
                    return Results.Ok(result);
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("HeavyOperations")
            .WithName("CreateOrReplaceDigitalTwinsBatch")
            .WithSummary(
                "Creates or replaces digital twins in a batch operation. Payloads larger than "
                + "100 items are split internally into chunks; batches above the configurable "
                + "ceiling (Parameters:MaxBatchSize, default 2000) are rejected with a 400. "
                + "Larger imports must use an import job."
            )
            .Produces<BatchDigitalTwinResult>();

        // POST /digitaltwins/search - Hybrid search endpoint
        digitalTwinsGroup
            .MapPost(
                "/search",
                async (
                    [FromBody] DigitalTwinSearchRequest request,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await client.HybridSearchAsync(
                        request.Vector,
                        request.EmbeddingProperty ?? "embedding",
                        request.ModelFilter,
                        request.Limit ?? 10,
                        cancellationToken
                    );
                    return Results.Content(result, "application/json");
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)
            .RequireRateLimiting("LightOperations")
            .WithName("SearchDigitalTwins")
            .WithSummary(
                "Performs a hybrid search on digital twins using vector similarity and metadata filter."
            );

        // POST /digitaltwins/memory-search - Scoped vector memory search.
        // Scope predicates are applied in the database before ranking and Limit.
        // Scope attributes are caller-managed twin properties; cross-graph fan-out
        // and owner-level access control stay outside this API (see how-to guide).
        digitalTwinsGroup
            .MapPost(
                "/memory-search",
                async Task<
                    Results<Ok<List<MemorySearchResultDto>>, ValidationProblem, ProblemHttpResult>
                > (
                    [FromBody] MemorySearchRequest request,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    if (request.Vector == null)
                    {
                        return TypedResults.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                ["vector"] = ["Query vector is required."],
                            }
                        );
                    }

                    string embeddingProperty = request.EmbeddingProperty ?? "embedding";
                    int excerptLength = request.ExcerptLength ?? 500;
                    if (excerptLength < 1 || excerptLength > 4000)
                    {
                        return TypedResults.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                ["excerptLength"] = ["Excerpt length must be between 1 and 4000."],
                            }
                        );
                    }

                    var options = new MemorySearchOptions
                    {
                        Vector = request.Vector,
                        EmbeddingProperty = embeddingProperty,
                        Limit = request.Limit ?? 10,
                        ModelIds = request.ModelIds,
                        PropertyFilters = request.PropertyFilters,
                        RelatedTwinId = request.RelatedTwinId,
                        ExpectedDimension = request.ExpectedDimension,
                    };

                    IReadOnlyList<MemorySearchResult> results;
                    try
                    {
                        results = await client.MemorySearchAsync(options, cancellationToken);
                    }
                    catch (ValidationFailedException ex)
                    {
                        return TypedResults.ValidationProblem(
                            new Dictionary<string, string[]> { ["request"] = [ex.Message] }
                        );
                    }
                    catch (PgVectorNotAvailableException ex)
                    {
                        return TypedResults.Problem(
                            detail: ex.Message,
                            statusCode: StatusCodes.Status503ServiceUnavailable
                        );
                    }

                    var response = results
                        .Select(r => new MemorySearchResultDto(
                            r.Twin.Id,
                            r.Twin.Metadata?.ModelId,
                            r.Distance,
                            BuildMemorySearchExcerpt(r.Twin, embeddingProperty, excerptLength),
                            r.Twin.LastUpdatedOn
                        ))
                        .ToList();

                    return TypedResults.Ok(response);
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)
            .RequireRateLimiting("LightOperations")
            .WithName("SearchDigitalTwinMemory")
            .WithSummary(
                "Performs a scoped vector search over digital twins. Scope predicates "
                    + "(model allow-list, property equality filters, related-twin predicate) "
                    + "are applied before ranking and Limit."
            )
            .Produces<List<MemorySearchResultDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // GET /digitaltwins/memory-search/capability - Report whether the backing
        // database can serve scoped vector memory search (pgvector availability).
        digitalTwinsGroup
            .MapGet(
                "/memory-search/capability",
                async Task<Ok<MemorySearchCapability>> (
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    bool available = await client.IsVectorSearchAvailableAsync(cancellationToken);
                    return TypedResults.Ok(new MemorySearchCapability(available));
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)
            .RequireRateLimiting("LightOperations")
            .WithName("GetDigitalTwinMemorySearchCapability")
            .WithSummary(
                "Reports whether pgvector is available for scoped vector memory search. "
                    + "Search and index operations return 503 when it is not."
            )
            .Produces<MemorySearchCapability>();

        // POST /digitaltwins/memory-search/index - Create the HNSW index behind
        // scoped vector memory search. Idempotent.
        digitalTwinsGroup
            .MapPost(
                "/memory-search/index",
                async Task<Results<Ok<MemorySearchIndex>, ValidationProblem, ProblemHttpResult>> (
                    [FromBody] MemorySearchIndexRequest request,
                    [FromServices] AgeDigitalTwinsClient client,
                    CancellationToken cancellationToken
                ) =>
                {
                    if (request.Dimension == null)
                    {
                        return TypedResults.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                ["dimension"] = ["Index dimension is required."],
                            }
                        );
                    }

                    var options = new MemorySearchIndexOptions
                    {
                        EmbeddingProperty = request.EmbeddingProperty ?? "embedding",
                        Dimension = request.Dimension.Value,
                        M = request.M,
                        EfConstruction = request.EfConstruction,
                    };

                    string indexName;
                    try
                    {
                        indexName = await client.EnsureMemorySearchIndexAsync(
                            options,
                            cancellationToken
                        );
                    }
                    catch (ValidationFailedException ex)
                    {
                        return TypedResults.ValidationProblem(
                            new Dictionary<string, string[]> { ["request"] = [ex.Message] }
                        );
                    }
                    catch (PgVectorNotAvailableException ex)
                    {
                        return TypedResults.Problem(
                            detail: ex.Message,
                            statusCode: StatusCodes.Status503ServiceUnavailable
                        );
                    }

                    return TypedResults.Ok(
                        new MemorySearchIndex(indexName, options.Dimension)
                    );
                }
            )
            .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write)
            .RequireRateLimiting("HeavyOperations")
            .WithName("EnsureDigitalTwinMemorySearchIndex")
            .WithSummary(
                "Creates the HNSW index for scoped vector memory search (idempotent). "
                    + "The dimension must match the stored embedding vectors."
            )
            .Produces<MemorySearchIndex>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static string BuildMemorySearchExcerpt(
        BasicDigitalTwin twin,
        string embeddingProperty,
        int maxLength
    )
    {
        var contents = twin.Contents
            .Where(kv => !string.Equals(kv.Key, embeddingProperty, StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        string excerpt = JsonSerializer.Serialize(contents);
        return excerpt.Length <= maxLength ? excerpt : excerpt[..maxLength];
    }
}
