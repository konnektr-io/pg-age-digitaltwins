using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Jobs.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    private ImportJobManager? _jobManager;
    private IMemoryCache? _memoryCache;
    private ILogger<ImportJobManager>? _importJobLogger;

    /// <summary>
    /// Gets or sets the memory cache instance used for job management.
    /// </summary>
    public IMemoryCache? MemoryCache
    {
        get => _memoryCache;
        set
        {
            _memoryCache = value;
            if (value != null && _importJobLogger != null)
            {
                _jobManager = new ImportJobManager(this, value, _importJobLogger);
            }
        }
    }

    /// <summary>
    /// Gets or sets the logger instance used for import job management.
    /// </summary>
    public ILogger<ImportJobManager>? ImportJobLogger
    {
        get => _importJobLogger;
        set
        {
            _importJobLogger = value;
            if (value != null && _memoryCache != null)
            {
                _jobManager = new ImportJobManager(this, _memoryCache, value);
            }
        }
    }

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
    public virtual async Task<ImportJobResult> ImportAsync(
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
    /// <param name="inputStream">The input stream containing ND-JSON data.</param>
    /// <param name="outputStream">The output stream for logging and progress.</param>
    /// <param name="options">Optional import job options.</param>
    /// <param name="jobId">Optional job ID. If not provided, a random ID will be generated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import job result with initial status (NotStarted or Running).</returns>
    /// <exception cref="InvalidOperationException">Thrown when job manager is not configured or job ID already exists.</exception>
    /// <exception cref="ArgumentNullException">Thrown when input or output stream is null.</exception>
    public virtual async Task<ImportJobResult> CreateImportJobAsync(
        Stream inputStream,
        Stream outputStream,
        ImportJobOptions? options = null,
        string? jobId = null,
        CancellationToken cancellationToken = default
    )
    {
        if (_jobManager == null)
            throw new InvalidOperationException(
                "Job manager is not configured. Please set both MemoryCache and ImportJobLogger properties."
            );

        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));

        if (outputStream == null)
            throw new ArgumentNullException(nameof(outputStream));

        jobId ??= Guid.NewGuid().ToString("N")[..8];

        return await _jobManager.CreateImportJobAsync(
            jobId,
            inputStream,
            outputStream,
            options,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets an import job by ID.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The import job result if found; otherwise null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job manager is not configured.</exception>
    public virtual ImportJobResult? GetImportJob(string jobId)
    {
        if (_jobManager == null)
            throw new InvalidOperationException(
                "Job manager is not configured. Please set both MemoryCache and ImportJobLogger properties."
            );

        return _jobManager.GetImportJob(jobId);
    }

    /// <summary>
    /// Lists all import jobs.
    /// </summary>
    /// <returns>A collection of all import jobs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job manager is not configured.</exception>
    public virtual IEnumerable<ImportJobResult> ListImportJobs()
    {
        if (_jobManager == null)
            throw new InvalidOperationException(
                "Job manager is not configured. Please set both MemoryCache and ImportJobLogger properties."
            );

        return _jobManager.ListImportJobs();
    }

    /// <summary>
    /// Cancels an import job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and cancellation was requested; otherwise false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job manager is not configured.</exception>
    public virtual bool CancelImportJob(string jobId)
    {
        if (_jobManager == null)
            throw new InvalidOperationException(
                "Job manager is not configured. Please set both MemoryCache and ImportJobLogger properties."
            );

        return _jobManager.CancelImportJob(jobId);
    }

    /// <summary>
    /// Deletes an import job from the cache.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>True if the job was found and deleted; otherwise false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when job manager is not configured.</exception>
    public virtual bool DeleteImportJob(string jobId)
    {
        if (_jobManager == null)
            throw new InvalidOperationException(
                "Job manager is not configured. Please set both MemoryCache and ImportJobLogger properties."
            );

        return _jobManager.DeleteImportJob(jobId);
    }
}
