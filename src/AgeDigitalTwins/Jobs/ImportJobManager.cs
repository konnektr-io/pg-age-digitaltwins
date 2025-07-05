using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Manages import jobs with in-memory caching and background processing.
/// </summary>
public class ImportJobManager
{
    private readonly AgeDigitalTwinsClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ImportJobManager>? _logger;
    private readonly ConcurrentDictionary<string, ImportJobResult> _jobs;
    private readonly TimeSpan _defaultJobRetention = TimeSpan.FromHours(24);

    public ImportJobManager(
        AgeDigitalTwinsClient client,
        IMemoryCache cache,
        ILogger<ImportJobManager>? logger = null
    )
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger;
        _jobs = new ConcurrentDictionary<string, ImportJobResult>();
    }

    /// <summary>
    /// Creates and starts a new import job.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="inputStream">The input stream containing the ND-JSON data.</param>
    /// <param name="outputStream">The output stream for logging and progress.</param>
    /// <param name="options">Optional import job options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import job result with initial status.</returns>
    public async Task<ImportJobResult> CreateImportJobAsync(
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

        // Check if job already exists
        if (_jobs.ContainsKey(jobId))
            throw new InvalidOperationException($"Import job with ID '{jobId}' already exists.");

        // Create job result with initial status
        var jobResult = new ImportJobResult
        {
            Id = jobId,
            InputBlobUri = null, // Set by API service when using blob URIs
            OutputBlobUri = null, // Set by API service when using blob URIs
            Status = ImportJobStatus.NotStarted,
            CreatedDateTime = DateTime.UtcNow,
            LastActionDateTime = DateTime.UtcNow,
            PurgeDateTime = DateTime.UtcNow.Add(_defaultJobRetention),
            CancellationTokenSource = new CancellationTokenSource(),
        };

        // Store in memory cache and dictionary
        _jobs[jobId] = jobResult;
        _cache.Set(GetCacheKey(jobId), jobResult, _defaultJobRetention);

        // Start the job in the background
        _ = Task.Run(async () =>
        {
            try
            {
                jobResult.Status = ImportJobStatus.Running;
                jobResult.LastActionDateTime = DateTime.UtcNow;
                UpdateJobInCache(jobResult);

                _logger?.LogInformation("Starting import job {JobId}", jobId);

                try
                {
                    // Execute the import job
                    var result = await StreamingImportJob.ExecuteAsync(
                        _client,
                        inputStream,
                        outputStream,
                        jobId,
                        options ?? new ImportJobOptions(),
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken,
                                jobResult.CancellationTokenSource.Token
                            )
                            .Token
                    );

                    // Update job result with execution results
                    jobResult.Status = result.Status;
                    jobResult.ModelsCreated = result.ModelsCreated;
                    jobResult.TwinsCreated = result.TwinsCreated;
                    jobResult.RelationshipsCreated = result.RelationshipsCreated;
                    jobResult.ErrorCount = result.ErrorCount;
                    jobResult.FinishedDateTime = DateTime.UtcNow;
                    jobResult.LastActionDateTime = DateTime.UtcNow;

                    if (result.ErrorCount > 0)
                    {
                        jobResult.Error = new ImportJobError
                        {
                            Code = "ProcessingErrors",
                            Message = $"Import completed with {result.ErrorCount} errors.",
                        };
                    }

                    _logger?.LogInformation(
                        "Import job {JobId} completed with status {Status}",
                        jobId,
                        result.Status
                    );
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing import job {JobId}", jobId);
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                jobResult.Status = ImportJobStatus.Cancelled;
                jobResult.FinishedDateTime = DateTime.UtcNow;
                jobResult.LastActionDateTime = DateTime.UtcNow;
                jobResult.Error = new ImportJobError
                {
                    Code = "Cancelled",
                    Message = "Import job was cancelled.",
                };
                _logger?.LogInformation("Import job {JobId} was cancelled", jobId);
            }
            catch (Exception ex)
            {
                jobResult.Status = ImportJobStatus.Failed;
                jobResult.FinishedDateTime = DateTime.UtcNow;
                jobResult.LastActionDateTime = DateTime.UtcNow;
                jobResult.ErrorCount++;
                jobResult.Error = new ImportJobError
                {
                    Code = "UnexpectedError",
                    Message = ex.Message,
                };
                _logger?.LogError(ex, "Import job {JobId} failed with unexpected error", jobId);
            }
            finally
            {
                UpdateJobInCache(jobResult);
            }
        });

        return jobResult;
    }

    /// <summary>
    /// Gets an import job by ID.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The import job result if found; otherwise null.</returns>
    public ImportJobResult? GetImportJob(string jobId)
    {
        if (string.IsNullOrEmpty(jobId))
            return null;

        return _cache.Get<ImportJobResult>(GetCacheKey(jobId));
    }

    /// <summary>
    /// Lists all import jobs.
    /// </summary>
    /// <returns>A collection of all import jobs.</returns>
    public IEnumerable<ImportJobResult> ListImportJobs()
    {
        return _jobs.Values.ToList();
    }

    /// <summary>
    /// Cancels an import job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and cancellation was requested; otherwise false.</returns>
    public bool CancelImportJob(string jobId)
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var job = GetImportJob(jobId);
        if (job == null)
            return false;

        if (job.Status == ImportJobStatus.Running || job.Status == ImportJobStatus.NotStarted)
        {
            job.Status = ImportJobStatus.Cancelling;
            job.LastActionDateTime = DateTime.UtcNow;
            job.CancellationTokenSource?.Cancel();
            UpdateJobInCache(job);

            _logger?.LogInformation("Cancellation requested for import job {JobId}", jobId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Deletes an import job from the cache.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and deleted; otherwise false.</returns>
    public bool DeleteImportJob(string jobId)
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        _cache.Remove(GetCacheKey(jobId));
        var removed = _jobs.TryRemove(jobId, out _);

        if (removed)
        {
            _logger?.LogInformation("Import job {JobId} deleted from cache", jobId);
        }

        return removed;
    }

    private void UpdateJobInCache(ImportJobResult job)
    {
        _cache.Set(GetCacheKey(job.Id), job, _defaultJobRetention);
    }

    private static string GetCacheKey(string jobId) => $"import_job_{jobId}";
}
