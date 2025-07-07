using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Jobs.Models;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    /// <summary>
    /// Imports models, twins, and relationships from an ND-JSON stream synchronously.
    /// </summary>
    /// <param name="inputStream">The ND-JSON input stream containing the data to import.</param>
    /// <param name="outputStream">The output stream where structured log entries will be written.</param>
    /// <param name="options">Configuration options for the import job. If null, default options will be used.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the import job result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when inputStream or outputStream is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the input stream contains invalid data.</exception>
    /// <remarks>
    /// <para>
    /// This method executes the import job synchronously and waits for completion.
    /// For background job execution, use <see cref="CreateImportJobAsync"/> instead.
    /// </para>
    /// <para>
    /// The input stream should contain ND-JSON (Newline Delimited JSON) data with the following format:
    /// </para>
    /// <list type="number">
    /// <item>Header section (optional): Contains metadata like file version, author, organization</item>
    /// <item>Models section (optional): Contains DTDL model definitions</item>
    /// <item>Twins section (optional): Contains digital twin instances</item>
    /// <item>Relationships section (optional): Contains relationships between twins</item>
    /// </list>
    /// <para>
    /// Each section is indicated by a JSON line with a "Section" property, followed by the data lines for that section.
    /// </para>
    /// <para>
    /// Example input format:
    /// </para>
    /// <code>
    /// {"Section": "Header"}
    /// {"fileVersion": "1.0.0", "author": "user", "organization": "company"}
    /// {"Section": "Models"}
    /// {"@id":"dtmi:example:model;1","@type":"Interface",...}
    /// {"Section": "Twins"}
    /// {"$dtId":"twin1","$metadata":{"$model":"dtmi:example:model;1"},...}
    /// {"Section": "Relationships"}
    /// {"$dtId":"twin1","$relationshipId":"rel1","$targetId":"twin2",...}
    /// </code>
    /// <para>
    /// The output stream will receive structured log entries in JSON format documenting the progress and results of the import operation.
    /// </para>
    /// </remarks>
    public virtual async Task<JobRecord> ImportAsync(
        Stream inputStream,
        Stream outputStream,
        ImportJobOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));
        if (outputStream == null)
            throw new ArgumentNullException(nameof(outputStream));

        options ??= new ImportJobOptions();
        var jobId = Guid.NewGuid().ToString("N")[..8]; // Use first 8 characters for shorter job ID

        return await StreamingImportJob.ExecuteAsync(
            this,
            inputStream,
            outputStream,
            jobId,
            options,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates and starts a new import job in the background.
    /// </summary>
    /// <param name="jobId">Optional job ID. If not provided, a random ID will be generated.</param>
    /// <param name="inputStream">The input stream containing ND-JSON data.</param>
    /// <param name="outputStream">The output stream for logging and progress.</param>
    /// <param name="options">Optional import job options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import job result with initial status (NotStarted or Running).</returns>
    /// <exception cref="InvalidOperationException">Thrown when job service is not configured or job ID already exists.</exception>
    /// <exception cref="ArgumentNullException">Thrown when input or output stream is null.</exception>
    public virtual async Task<JobRecord> CreateImportJobAsync(
        string jobId,
        Stream inputStream,
        Stream outputStream,
        ImportJobOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));

        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));

        if (outputStream == null)
            throw new ArgumentNullException(nameof(outputStream));

        // Create the job request with blob URIs (will be set by API service)
        var request = new ImportJobRequest
        {
            InputBlobUri = null, // Will be set by API service
            OutputBlobUri = null, // Will be set by API service
            Options = options,
        };

        // Create the job record
        var jobRecord = await JobService.CreateJobAsync(jobId, "import", request, cancellationToken);

        // Start the import job in the background
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await JobService.UpdateJobStatusAsync(
                        jobId,
                        JobStatus.Running,
                        cancellationToken: cancellationToken
                    );

                    // Execute the streaming import job
                    var result = await StreamingImportJob.ExecuteAsync(
                        this,
                        inputStream,
                        outputStream,
                        jobId,
                        options ?? new ImportJobOptions(),
                        cancellationToken
                    );

                    // Update job with results
                    var resultData = new
                    {
                        ModelsCreated = result.ModelsCreated,
                        TwinsCreated = result.TwinsCreated,
                        RelationshipsCreated = result.RelationshipsCreated,
                        ErrorCount = result.ErrorCount,
                        Status = result.Status.ToString(),
                        FinishedDateTime = result.FinishedDateTime,
                    };

                    await JobService.CompleteJobAsync(
                        jobId,
                        resultData,
                        cancellationToken: cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    var errorData = new
                    {
                        Code = "UnexpectedError",
                        Message = ex.Message,
                        StackTrace = ex.StackTrace,
                    };

                    await JobService.FailJobAsync(jobId, errorData, cancellationToken: cancellationToken);
                }
            },
            cancellationToken
        );

        // Return the job record directly
        return jobRecord;
    }

    /// <summary>
    /// Gets an import job by ID.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The job record if found; otherwise null.</returns>
    public virtual JobRecord? GetImportJob(string jobId)
    {
        var jobRecord = JobService.GetJobAsync(jobId).GetAwaiter().GetResult();
        return jobRecord;
    }

    /// <summary>
    /// Lists all import jobs.
    /// </summary>
    /// <returns>A collection of all import jobs.</returns>
    public virtual IEnumerable<JobRecord> ListImportJobs()
    {
        var jobRecords = JobService.ListJobsAsync("import").GetAwaiter().GetResult();
        return jobRecords;
    }

    /// <summary>
    /// Cancels an import job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and cancellation was requested; otherwise false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job service is not configured.</exception>
    public virtual bool CancelImportJob(string jobId)
    {
        try
        {
            JobService.UpdateJobStatusAsync(jobId, JobStatus.Cancelled).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes an import job from the job store.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and deleted; otherwise false.</returns>
    public virtual bool DeleteImportJob(string jobId)
    {
        try
        {
            JobService.DeleteJobAsync(jobId).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
