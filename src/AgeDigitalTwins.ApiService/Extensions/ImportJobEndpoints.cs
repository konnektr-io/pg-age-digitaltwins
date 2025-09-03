using System.ComponentModel.DataAnnotations;
using AgeDigitalTwins.ApiService.Models;
using AgeDigitalTwins.ApiService.Services;
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
            .WithDescription("Import job management endpoints")
            .RequireRateLimiting("AdminOperations");

        // Create/Start Import Job
        jobs.MapPut("/{id}", CreateImportJobAsync)
            .RequireAuthorization()
            .WithName("CreateImportJob")
            .WithSummary("Create and start a new import job")
            .WithDescription(
                "Creates and starts a new import job. The job will run in the background and return immediately. Monitor job progress using the GetImportJob endpoint."
            )
            .Produces<ImportJob>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Get Import Job by ID
        jobs.MapGet("/{id}", GetImportJobAsync)
            .WithName("GetImportJob")
            .WithSummary("Get import job by ID")
            .WithDescription("Retrieves the status and details of an import job by its ID.")
            .Produces<ImportJob>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        // List Import Jobs
        jobs.MapGet("/", ListImportJobsAsync)
            .RequireAuthorization()
            .WithName("ListImportJobs")
            .WithSummary("List all import jobs")
            .WithDescription("Retrieves a list of all import jobs with optional pagination.")
            .Produces<PageWithNextLink<ImportJob>>();

        // Cancel Import Job
        jobs.MapPost("/{id}/cancel", CancelImportJobAsync)
            .RequireAuthorization()
            .WithName("CancelImportJob")
            .WithSummary("Cancel an import job")
            .WithDescription(
                "Cancels a running import job. Note that this will leave your instance in an unknown state as there won't be any rollback operation."
            )
            .Produces<ImportJob>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Delete Import Job
        jobs.MapDelete("/{id}", DeleteImportJobAsync)
            .RequireAuthorization()
            .WithName("DeleteImportJob")
            .WithSummary("Delete an import job")
            .WithDescription(
                "Removes an import job from the system. This is a non-standard endpoint for cleanup purposes."
            )
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Resume Import Job
        jobs.MapPost("/{id}/resume", ResumeImportJobAsync)
            .RequireAuthorization()
            .WithName("ResumeImportJob")
            .WithSummary("Resume an import job")
            .WithDescription(
                "Resumes an interrupted import job from its last checkpoint. The job must be in a resumable state (Running or Failed)."
            )
            .Produces<ImportJob>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<
        Results<Created<ImportJob>, ValidationProblem, ProblemHttpResult, Conflict>
    > CreateImportJobAsync(
        [Required] string id,
        [FromBody] ImportJobRequest request,
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
            // Create a stream factory that will be called within the background task
            Func<
                CancellationToken,
                Task<(Stream inputStream, Stream outputStream)>
            > streamFactory = async (ct) =>
            {
                var inputStream = await blobStorageService.GetReadStreamAsync(request.InputBlobUri);
                var outputStream = await blobStorageService.GetWriteStreamAsync(
                    request.OutputBlobUri
                );
                return (inputStream, outputStream);
            };

            // Execute the import job with background execution
            var result = await client.ImportGraphAsync(
                id,
                streamFactory,
                options: null, // API doesn't provide these options currently
                request,
                executeInBackground: true,
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

    private static async Task<Results<Ok<ImportJob>, NotFound>> GetImportJobAsync(
        [Required] string id,
        [FromServices] AgeDigitalTwinsClient client
    )
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var job = await client.GetImportJobAsync(id);

        if (job == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new ImportJob(job));
    }

    private static async Task<Ok<PageWithNextLink<ImportJob?>>> ListImportJobsAsync(
        HttpContext httpContext,
        [FromServices] AgeDigitalTwinsClient client
    )
    {
        var jobs = (await client.GetImportJobsAsync()).Select(job => new ImportJob(job));
        var page = new Page<ImportJob?>() { Value = jobs };

        return TypedResults.Ok(new PageWithNextLink<ImportJob?>(page, httpContext.Request));
    }

    private static async Task<
        Results<Ok<ImportJob>, NotFound, ProblemHttpResult>
    > CancelImportJobAsync([Required] string id, [FromServices] AgeDigitalTwinsClient client)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var job = await client.GetImportJobAsync(id);

        if (job == null)
            return TypedResults.NotFound();

        if (job.Status != JobStatus.Running && job.Status != JobStatus.Notstarted)
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
        return TypedResults.Ok(new ImportJob(updatedJob!));
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

    private static async Task<
        Results<Ok<ImportJob>, NotFound, ProblemHttpResult>
    > ResumeImportJobAsync(
        [Required] string id,
        [FromServices] AgeDigitalTwinsClient client,
        [FromServices] IBlobStorageService blobStorageService,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.NotFound();

        var job = await client.GetImportJobAsync(id);
        if (job == null)
            return TypedResults.NotFound();

        // Check if job is in a resumable state
        if (job.Status != JobStatus.Running && job.Status != JobStatus.Failed)
        {
            return TypedResults.Problem(
                detail: $"Cannot resume job in status '{job.Status}'. Only running or failed jobs can be resumed.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Failed"
            );
        }

        // Check if job is already locked by another instance
        var isLocked = await client.IsJobLockedByAnotherInstanceAsync(id, cancellationToken);
        if (isLocked)
        {
            return TypedResults.Problem(
                detail: "Job is currently being processed by another instance.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Job Locked"
            );
        }

        try
        {
            // Get blob URIs from job record
            var inputBlobUri = job.InputBlobUri;
            var outputBlobUri = job.OutputBlobUri;

            if (string.IsNullOrEmpty(inputBlobUri) || string.IsNullOrEmpty(outputBlobUri))
            {
                return TypedResults.Problem(
                    detail: "Job is missing required blob URIs for resumption.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Job State"
                );
            }

            // Get blob streams
            await using var inputStream = await blobStorageService.GetReadStreamAsync(
                new Uri(inputBlobUri)
            );
            await using var outputStream = await blobStorageService.GetWriteStreamAsync(
                new Uri(outputBlobUri)
            );

            // Resume the job
            var result = await client.ResumeImportJobAsync(
                id,
                inputStream,
                outputStream,
                cancellationToken
            );

            return TypedResults.Ok(new ImportJob(result));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Resume Failed"
            );
        }
        catch (Exception ex)
        {
            // Log the exception here if needed
            return TypedResults.Problem(
                detail: $"An unexpected error occurred while resuming the job: {ex.Message}",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error"
            );
        }
    }
}
