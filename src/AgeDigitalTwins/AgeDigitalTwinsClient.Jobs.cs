using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    /// <summary>
    /// Creates and executes an import job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="inputStream">The input stream containing ND-JSON data.</param>
    /// <param name="outputStream">The output stream for logging and progress.</param>
    /// <param name="request">Original import job request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import job result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job service is not configured or job ID already exists.</exception>
    /// <exception cref="ArgumentNullException">Thrown when input or output stream is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the input stream contains invalid data.</exception>
    public async virtual Task<JobRecord> ImportGraphAsync<TRequest>(
        string jobId,
        Stream inputStream,
        Stream outputStream,
        TRequest? request = default,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));

        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));

        if (outputStream == null)
            throw new ArgumentNullException(nameof(outputStream));

        // Create the job record
        var jobRecord = await JobService.CreateJobAsync(
            jobId,
            "import",
            request,
            cancellationToken
        );

        // Check if there's an existing checkpoint for this job
        var checkpoint = await JobService.LoadCheckpointAsync(jobId, cancellationToken);

        // Start job execution with checkpoint support
        try
        {
            var result = await StreamingImportJob.ExecuteWithCheckpointAsync(
                this,
                inputStream,
                outputStream,
                jobId,
                checkpoint,
                cancellationToken
            );

            // Update job record with final result
            await JobService.UpdateJobStatusAsync(
                jobId,
                result.Status,
                resultData: new
                {
                    modelsCreated = result.ModelsCreated,
                    twinsCreated = result.TwinsCreated,
                    relationshipsCreated = result.RelationshipsCreated,
                    errorCount = result.ErrorCount,
                },
                cancellationToken: cancellationToken
            );

            // Return the updated job record from the database
            return await JobService.GetJobAsync(jobId) ?? result;
        }
        catch (Exception ex)
        {
            // Update job status to failed if an exception occurs
            await JobService.UpdateJobStatusAsync(
                jobId,
                JobStatus.Failed,
                errorData: new { error = ex.Message },
                cancellationToken: cancellationToken
            );
            throw;
        }
    }

    /// <summary>
    /// Gets an import job by ID.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The job record if found; otherwise null.</returns>
    public async virtual Task<JobRecord?> GetImportJobAsync(string jobId)
    {
        return await JobService.GetJobAsync(jobId);
    }

    /// <summary>
    /// Gets an import job by ID (synchronous version).
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The job record if found; otherwise null.</returns>
    public virtual JobRecord? GetImportJob(string jobId)
    {
        return GetImportJobAsync(jobId).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Lists all import jobs.
    /// </summary>
    /// <returns>A collection of all import jobs.</returns>
    public async virtual Task<IEnumerable<JobRecord>> GetImportJobsAsync()
    {
        return await JobService.ListJobsAsync("import");
    }

    /// <summary>
    /// Cancels an import job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and cancellation was requested; otherwise false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job service is not configured.</exception>
    public async virtual Task<bool> CancelImportJobAsync(string jobId)
    {
        try
        {
            await JobService.UpdateJobStatusAsync(jobId, JobStatus.Cancelled);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cancels an import job (synchronous version).
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and cancellation was requested; otherwise false.</returns>
    public virtual bool CancelImportJob(string jobId)
    {
        return CancelImportJobAsync(jobId).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Deletes an import job from the job store.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and deleted; otherwise false.</returns>
    public async virtual Task<bool> DeleteImportJobAsync(string jobId)
    {
        try
        {
            await JobService.DeleteJobAsync(jobId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes an import job from the job store (synchronous version).
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and deleted; otherwise false.</returns>
    public virtual bool DeleteImportJob(string jobId)
    {
        return DeleteImportJobAsync(jobId).GetAwaiter().GetResult();
    }
}
