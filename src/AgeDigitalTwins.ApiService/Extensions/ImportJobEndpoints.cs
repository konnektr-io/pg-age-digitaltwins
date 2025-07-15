using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AgeDigitalTwins.ApiService.Models;
using AgeDigitalTwins.ApiService.Services;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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
            .Produces<JobRecord>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Get Import Job by ID
        jobs.MapGet("/{id}", GetImportJobAsync)
            .WithName("GetImportJob")
            .WithSummary("Get import job by ID")
            .WithDescription("Retrieves the status and details of an import job by its ID.")
            .Produces<JobRecord>()
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
            .Produces<JobRecord>()
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
        Results<Created<ImportJob>, ValidationProblem, ProblemHttpResult, Conflict>
    > CreateImportJobAsync(
        [Required] string id,
        [FromBody] ImportJob request,
        [FromServices] AgeDigitalTwinsClient client,
        [FromServices] IBlobStorageService blobStorageService,
        CancellationToken cancellationToken = default
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

        if (string.IsNullOrWhiteSpace(request.InputBlobUri.AbsoluteUri))
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["inputBlobUri"] = ["Input blob URI is required."],
                }
            );

        if (string.IsNullOrWhiteSpace(request.OutputBlobUri.AbsoluteUri))
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

            var result = await client.ImportGraphAsync(
                id,
                inputStream,
                outputStream,
                request,
                cancellationToken
            );

            if (result == null)
            {
                throw new InvalidOperationException($"Import job with ID '{id}' already exists.");
            }

            return TypedResults.Created($"/jobs/imports/{id}", new ImportJob(result));
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
        catch (Exception)
        {
            // Log error - could inject ILogger if needed, but for now just return the error response
            return TypedResults.Problem(
                detail: "An unexpected error occurred while creating the import job.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error"
            );
        }
    }

    private static async Task<Results<Ok<JobRecord>, NotFound>> GetImportJobAsync(
        [Required] string id,
        [FromServices] AgeDigitalTwinsClient client
    )
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var job = await client.GetImportJobAsync(id);

        if (job == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(job);
    }

    private static async Task<Ok<ImportJobCollection>> ListImportJobsAsync(
        [FromServices] AgeDigitalTwinsClient client,
        [FromQuery] int? maxItemsPerPage = null
    )
    {
        var jobs = (await client.GetImportJobsAsync()).ToList();

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

    private static async Task<
        Results<Ok<JobRecord>, NotFound, ProblemHttpResult>
    > CancelImportJobAsync([Required] string id, [FromServices] AgeDigitalTwinsClient client)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var job = await client.GetImportJobAsync(id);

        if (job == null)
            return TypedResults.NotFound();

        if (job.Status != JobStatus.Running && job.Status != JobStatus.NotStarted)
        {
            return TypedResults.Problem(
                detail: $"Cannot cancel job in status '{job.Status}'. Only running or not started jobs can be cancelled.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Failed"
            );
        }

        var cancelled = await client.CancelImportJobAsync(id);
        if (!cancelled)
        {
            return TypedResults.Problem(
                detail: "Failed to cancel the import job.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Cancellation Failed"
            );
        }

        // Return updated job status
        var updatedJob = await client.GetImportJobAsync(id);
        return TypedResults.Ok(updatedJob!);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteImportJobAsync(
        [Required] string id,
        [FromServices] AgeDigitalTwinsClient client
    )
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var deleted = await client.DeleteImportJobAsync(id);

        if (!deleted)
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }
}
