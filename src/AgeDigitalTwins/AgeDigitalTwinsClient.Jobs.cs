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
    /// <param name="options">Import job options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import job result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job service is not configured or job ID already exists.</exception>
    /// <exception cref="ArgumentNullException">Thrown when input or output stream is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the input stream contains invalid data.</exception>
    public async virtual Task<JobRecord> ImportGraphAsync(
        string jobId,
        Stream inputStream,
        Stream outputStream,
        ImportJobOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        return await ImportGraphAsync<object>(
            jobId,
            inputStream,
            outputStream,
            options,
            request: null,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates and executes an import job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="inputStream">The input stream containing ND-JSON data.</param>
    /// <param name="outputStream">The output stream for logging and progress.</param>
    /// <param name="options">Import job options.</param>
    /// <param name="request">Original import job request to store in database.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import job result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job service is not configured or job ID already exists.</exception>
    /// <exception cref="ArgumentNullException">Thrown when input or output stream is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the input stream contains invalid data.</exception>
    public async virtual Task<JobRecord> ImportGraphAsync<TRequest>(
        string jobId,
        Stream inputStream,
        Stream outputStream,
        ImportJobOptions? options = null,
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

        // Use the options if provided, otherwise create default options
        options ??= new ImportJobOptions();

        // Create a stream factory that returns the provided streams
        Func<CancellationToken, Task<(Stream inputStream, Stream outputStream)>> streamFactory = (
            ct
        ) => Task.FromResult((inputStream, outputStream));

        // Delegate to the factory-based method with synchronous execution and stream disposal based on options
        return await ImportGraphAsync(
            jobId,
            streamFactory,
            options,
            request,
            executeInBackground: false,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates and executes an import job using stream factories for background execution.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="streamFactory">Factory function that creates input and output streams.</param>
    /// <param name="options">Import job options.</param>
    /// <param name="request">Original import job request to store in database.</param>
    /// <param name="executeInBackground">Whether to execute the job in background.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import job result.</returns>
    public async virtual Task<JobRecord> ImportGraphAsync<TRequest>(
        string jobId,
        Func<CancellationToken, Task<(Stream inputStream, Stream outputStream)>> streamFactory,
        ImportJobOptions? options = null,
        TRequest? request = default,
        bool executeInBackground = false,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));

        if (streamFactory == null)
            throw new ArgumentNullException(nameof(streamFactory));

        // Use the options if provided, otherwise create default options
        options ??= new ImportJobOptions();

        // For background execution, always dispose streams
        // For synchronous execution, respect the LeaveOpen option
        bool shouldDisposeStreams = executeInBackground || !options.LeaveOpen;

        // Check if job already exists
        var existingJob = await JobService.GetJobAsync(jobId, cancellationToken);

        if (existingJob != null)
        {
            // Job already exists - throw exception
            throw new InvalidOperationException($"Job with ID '{jobId}' already exists.");
        }

        // Create new job record
        var jobRecord = await JobService.CreateJobAsync(
            jobId,
            "import",
            request,
            cancellationToken
        );

        // Try to acquire a distributed lock for the job
        var lockAcquired = await JobService.TryAcquireJobLockAsync(
            jobId,
            cancellationToken: cancellationToken
        );
        if (!lockAcquired)
        {
            throw new InvalidOperationException(
                $"Failed to acquire lock for job {jobId}. Job may already be running on another instance."
            );
        }

        try
        {
            // Check if there's an existing checkpoint for this job
            var checkpoint = await JobService.LoadCheckpointAsync(jobId, cancellationToken);

            if (executeInBackground)
            {
                // For background execution, update job status to Running and start the job
                await JobService.UpdateJobStatusAsync(
                    jobId,
                    JobStatus.Running,
                    cancellationToken: cancellationToken
                );

                // Update the job record to reflect the new status
                jobRecord = await JobService.GetJobAsync(jobId, cancellationToken) ?? jobRecord;

                // Start the job execution in the background with proper stream lifecycle
                _ = Task.Run(
                    async () =>
                    {
                        try
                        {
                            // Create streams within the background task
                            var (inputStream, outputStream) = await streamFactory(
                                cancellationToken
                            );

                            await using (inputStream)
                            await using (outputStream)
                            {
                                // Start job execution with checkpoint support
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
                                    errorData: result.Error,
                                    cancellationToken: cancellationToken
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            // Update job status to failed if an exception occurs
                            await JobService.UpdateJobStatusAsync(
                                jobId,
                                JobStatus.Failed,
                                errorData: new ImportJobError
                                {
                                    Code = ex.GetType().Name,
                                    Message = ex.Message,
                                    Details = new Dictionary<string, object>
                                    {
                                        { "stackTrace", ex.StackTrace ?? string.Empty },
                                        { "timestamp", DateTime.UtcNow.ToString("o") },
                                    },
                                },
                                cancellationToken: cancellationToken
                            );
                        }
                        finally
                        {
                            // Always release the lock when done
                            await JobService.ReleaseJobLockAsync(jobId, cancellationToken);
                        }
                    },
                    cancellationToken
                );

                // Return the job record immediately for background execution
                return jobRecord;
            }
            else
            {
                // For synchronous execution, create streams and proceed
                var (inputStream, outputStream) = await streamFactory(cancellationToken);

                if (shouldDisposeStreams)
                {
                    await using (inputStream)
                    await using (outputStream)
                    {
                        // Start job execution with checkpoint support
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
                            errorData: result.Error,
                            cancellationToken: cancellationToken
                        );

                        // Get the updated job record
                        var updatedJob = await JobService.GetJobAsync(jobId, cancellationToken);
                        return updatedJob ?? jobRecord;
                    }
                }
                else
                {
                    // Don't dispose streams - they're owned by the caller
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
                        errorData: result.Error,
                        cancellationToken: cancellationToken
                    );

                    // Get the updated job record
                    var updatedJob = await JobService.GetJobAsync(jobId, cancellationToken);
                    return updatedJob ?? jobRecord;
                }
            }
        }
        catch (Exception ex)
        {
            // Update job status to failed if an exception occurs
            await JobService.UpdateJobStatusAsync(
                jobId,
                JobStatus.Failed,
                errorData: new ImportJobError
                {
                    Code = ex.GetType().Name,
                    Message = ex.Message,
                    Details = new Dictionary<string, object>
                    {
                        { "stackTrace", ex.StackTrace ?? string.Empty },
                        { "timestamp", DateTime.UtcNow.ToString("o") },
                    },
                },
                cancellationToken: cancellationToken
            );

            if (executeInBackground)
            {
                // For background execution, return the job record with failed status
                return jobRecord;
            }
            else
            {
                // For synchronous execution, re-throw the exception
                throw;
            }
        }
        finally
        {
            // Only release the lock for synchronous execution
            // For background execution, the lock is released in the background task
            if (!executeInBackground)
            {
                await JobService.ReleaseJobLockAsync(jobId, cancellationToken);
            }
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

    /// <summary>
    /// Resumes a previously interrupted import job using the distributed locking mechanism.
    /// This method should be called when a job needs to be resumed, potentially on a different instance.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The job record if the job was resumed successfully; otherwise null.</returns>
    public async Task<JobRecord?> ResumeImportJobAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));

        // Check if the job exists and is in a resumable state
        var existingJob = await JobService.GetJobAsync(jobId, cancellationToken);
        if (existingJob == null)
        {
            return null;
        }

        // Only resume jobs that are in running state or have failed due to instance failure
        if (existingJob.Status != JobStatus.Running && existingJob.Status != JobStatus.Failed)
        {
            return existingJob;
        }

        // Try to acquire the distributed lock
        var lockAcquired = await JobService.TryAcquireJobLockAsync(
            jobId,
            cancellationToken: cancellationToken
        );
        if (!lockAcquired)
        {
            // Job is already being processed by another instance
            return existingJob;
        }

        try
        {
            // Check if we have a checkpoint to resume from
            var checkpoint = await JobService.LoadCheckpointAsync(jobId, cancellationToken);
            if (checkpoint == null)
            {
                // No checkpoint available, cannot resume
                return existingJob;
            }

            // For resuming, we need the original input stream - this would typically
            // come from a stored location (e.g., blob storage, file system)
            // For now, we'll return the existing job and let the caller handle providing the stream
            return existingJob;
        }
        finally
        {
            // Release the lock if we're not going to process the job
            await JobService.ReleaseJobLockAsync(jobId, cancellationToken);
        }
    }

    /// <summary>
    /// Checks if a job is currently locked by another instance.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the job is locked by another instance; otherwise false.</returns>
    public async Task<bool> IsJobLockedByAnotherInstanceAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var lockInfo = await JobService.GetJobLockInfoAsync(jobId, cancellationToken);
        return lockInfo != null && !lockInfo.IsExpired;
    }

    /// <summary>
    /// Gets information about the job lock.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Lock information if available; otherwise null.</returns>
    public async Task<JobLockInfo?> GetJobLockInfoAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return null;

        return await JobService.GetJobLockInfoAsync(jobId, cancellationToken);
    }

    /// <summary>
    /// Gets all jobs that should be resumed on startup.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of job records that need to be resumed.</returns>
    public async Task<List<JobRecord>> GetJobsToResumeAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await JobService.GetJobsToResumeAsync(cancellationToken);
    }

    /// <summary>
    /// Resumes a specific import job from its checkpoint.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="inputStream">The input stream containing ND-JSON data.</param>
    /// <param name="outputStream">The output stream for logging and progress.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated job record.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job is not in a resumable state.</exception>
    public async Task<JobRecord> ResumeImportJobAsync(
        string jobId,
        Stream inputStream,
        Stream outputStream,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));

        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));

        if (outputStream == null)
            throw new ArgumentNullException(nameof(outputStream));

        // Check if we have a lock on this job
        var lockInfo = await JobService.GetJobLockInfoAsync(jobId, cancellationToken);
        if (lockInfo?.IsExpired != false)
        {
            throw new InvalidOperationException(
                $"Job {jobId} is not locked by this instance or lock has expired"
            );
        }

        // Load checkpoint for this job
        var checkpoint = await JobService.LoadCheckpointAsync(jobId, cancellationToken);
        if (checkpoint == null)
        {
            throw new InvalidOperationException($"No checkpoint found for job {jobId}");
        }

        // Resume execution from checkpoint
        var result = await StreamingImportJob.ExecuteWithCheckpointAsync(
            this,
            inputStream,
            outputStream,
            jobId,
            checkpoint,
            cancellationToken
        );

        // Update job record with final result
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
                resumed = true,
                resumedAt = DateTime.UtcNow,
            },
            errorData: result.Error,
            cancellationToken: cancellationToken
        );

        return result;
    }
}
