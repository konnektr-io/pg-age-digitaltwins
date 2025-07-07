using System.ComponentModel.DataAnnotations;
using AgeDigitalTwins.ApiService.Models;
using AgeDigitalTwins.ApiService.Services;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Jobs.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AgeDigitalTwins.ApiService.Extensions;

/// <summary>
/// Extension methods for mapping import job endpoints.
/// </summary>
public static class ImportJobEndpoints
{
    /// <summary>
    /// Maps import job endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapImportJobEndpoints(this WebApplication app)
    {
        var jobs = app.MapGroup("/jobs/imports")
            .WithTags("Import Jobs")
            .WithDescription(
                "Import job management endpoints compatible with Azure Digital Twins API"
            );

        // Create/Start Import Job
        jobs.MapPut("/{id}", CreateImportJobAsync)
            .WithName("CreateImportJob")
            .WithSummary("Create and start a new import job")
            .WithDescription(
                "Creates and starts a new import job. The job will run in the background and can be monitored using the GetImportJob endpoint."
            )
            .Produces<ImportJobResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Get Import Job by ID
        jobs.MapGet("/{id}", GetImportJobAsync)
            .WithName("GetImportJob")
            .WithSummary("Get import job by ID")
            .WithDescription("Retrieves the status and details of an import job by its ID.")
            .Produces<ImportJobResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // List Import Jobs
        jobs.MapGet("/", ListImportJobsAsync)
            .WithName("ListImportJobs")
            .WithSummary("List all import jobs")
            .WithDescription("Retrieves a list of all import jobs with optional pagination.")
            .Produces<ImportJobCollection>();

        // Cancel Import Job
        jobs.MapPost("/{id}/cancel", CancelImportJobAsync)
            .WithName("CancelImportJob")
            .WithSummary("Cancel an import job")
            .WithDescription(
                "Cancels a running import job. Note that this will leave your instance in an unknown state as there won't be any rollback operation."
            )
            .Produces<ImportJobResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Delete Import Job
        jobs.MapDelete("/{id}", DeleteImportJobAsync)
            .WithName("DeleteImportJob")
            .WithSummary("Delete an import job")
            .WithDescription(
                "Removes an import job from the system. This is a non-standard endpoint for cleanup purposes."
            )
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<
        Results<Created<ImportJobResult>, ValidationProblem, ProblemHttpResult, Conflict>
    > CreateImportJobAsync(
        [Required] string id,
        [FromBody] ImportJobRequest request,
        [FromServices] AgeDigitalTwinsClient client,
        [FromServices] IMemoryCache cache,
        [FromServices] ILogger<ImportJobManager> logger,
        [FromServices] IBlobStorageService blobStorageService,
        [FromQuery] string? apiVersion = "2023-10-31"
    )
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["id"] = ["Job ID cannot be null or empty."] }
            );

        if (request == null)
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["request"] = ["Import job request cannot be null."],
                }
            );

        if (string.IsNullOrWhiteSpace(request.InputBlobUri))
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["inputBlobUri"] = ["Input blob URI is required."],
                }
            );

        if (string.IsNullOrWhiteSpace(request.OutputBlobUri))
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["outputBlobUri"] = ["Output blob URI is required."],
                }
            );

        try
        {
            // Get streams from blob URIs
            using var inputStream = await blobStorageService.GetReadStreamAsync(
                request.InputBlobUri
            );
            using var outputStream = await blobStorageService.GetWriteStreamAsync(
                request.OutputBlobUri
            );

            var result = await client.CreateImportJobAsync(
                id,
                inputStream,
                outputStream,
                request.Options
            );

            // Update the result with blob URIs for the response
            result.InputBlobUri = request.InputBlobUri;
            result.OutputBlobUri = request.OutputBlobUri;

            return TypedResults.Created($"/jobs/imports/{id}", result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return TypedResults.Conflict();
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Failed"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create import job {JobId}", id);
            return TypedResults.Problem(
                detail: "An unexpected error occurred while creating the import job.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error"
            );
        }
    }

    private static Results<Ok<ImportJobResult>, NotFound> GetImportJobAsync(
        [Required] string id,
        [FromServices] AgeDigitalTwinsClient client,
        [FromServices] IMemoryCache cache,
        [FromServices] ILogger<ImportJobManager> logger,
        [FromQuery] string? apiVersion = "2023-10-31"
    )
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var job = client.GetImportJob(id);

        if (job == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(job);
    }

    private static Ok<ImportJobCollection> ListImportJobsAsync(
        [FromServices] AgeDigitalTwinsClient client,
        [FromServices] IMemoryCache cache,
        [FromServices] ILogger<ImportJobManager> logger,
        [FromQuery] int? maxItemsPerPage = null,
        [FromQuery] string? apiVersion = "2023-10-31"
    )
    {
        var jobs = client.ListImportJobs().ToList();

        // Apply pagination if requested
        if (maxItemsPerPage.HasValue && maxItemsPerPage.Value > 0)
        {
            jobs = jobs.Take(maxItemsPerPage.Value).ToList();
        }

        var collection = new ImportJobCollection
        {
            Value = jobs,
            NextLink =
                null // TODO: Implement pagination with next link if needed
            ,
        };

        return TypedResults.Ok(collection);
    }

    private static Results<Ok<ImportJobResult>, NotFound, ProblemHttpResult> CancelImportJobAsync(
        [Required] string id,
        [FromServices] AgeDigitalTwinsClient client,
        [FromServices] IMemoryCache cache,
        [FromServices] ILogger<ImportJobManager> logger,
        [FromQuery] string? apiVersion = "2023-10-31"
    )
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var job = client.GetImportJob(id);

        if (job == null)
            return TypedResults.NotFound();

        if (job.Status != ImportJobStatus.Running && job.Status != ImportJobStatus.NotStarted)
        {
            return TypedResults.Problem(
                detail: $"Cannot cancel job in status '{job.Status}'. Only running or not started jobs can be cancelled.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Failed"
            );
        }

        var cancelled = client.CancelImportJob(id);
        if (!cancelled)
        {
            return TypedResults.Problem(
                detail: "Failed to cancel the import job.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Cancellation Failed"
            );
        }

        // Return updated job status
        var updatedJob = client.GetImportJob(id);
        return TypedResults.Ok(updatedJob!);
    }

    private static Results<NoContent, NotFound> DeleteImportJobAsync(
        [Required] string id,
        [FromServices] AgeDigitalTwinsClient client,
        [FromServices] IMemoryCache cache,
        [FromServices] ILogger<ImportJobManager> logger,
        [FromQuery] string? apiVersion = "2023-10-31"
    )
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var deleted = client.DeleteImportJob(id);

        if (!deleted)
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }
}
