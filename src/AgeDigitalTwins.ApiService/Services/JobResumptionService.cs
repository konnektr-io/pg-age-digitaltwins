using AgeDigitalTwins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ApiService.Services;

/// <summary>
/// Background service that resumes incomplete jobs on application startup.
/// </summary>
public class JobResumptionService : BackgroundService
{
    private readonly AgeDigitalTwinsClient _client;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<JobResumptionService> _logger;

    public JobResumptionService(
        AgeDigitalTwinsClient client,
        IBlobStorageService blobStorageService,
        ILogger<JobResumptionService> logger
    )
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _blobStorageService =
            blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting job resumption service");

            // Wait a bit to ensure the application is fully started
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            // Find jobs that need to be resumed
            var jobsToResume = await _client.GetJobsToResumeAsync(stoppingToken);

            if (jobsToResume.Count == 0)
            {
                _logger.LogInformation("No incomplete jobs found to resume");
                return;
            }

            _logger.LogInformation(
                "Found {JobCount} incomplete jobs to resume",
                jobsToResume.Count
            );

            // Resume each job
            foreach (var job in jobsToResume)
            {
                try
                {
                    _logger.LogInformation(
                        "Attempting to resume job {JobId} of type {JobType} (status: {Status})",
                        job.Id,
                        job.JobType,
                        job.Status
                    );

                    // Skip if not an import job
                    if (job.JobType != "import")
                    {
                        _logger.LogWarning(
                            "Job {JobId} is not an import job (type: {JobType}), skipping resumption",
                            job.Id,
                            job.JobType
                        );
                        continue;
                    }

                    // Get blob URIs from job request data
                    var inputBlobUri = job.InputBlobUri;
                    var outputBlobUri = job.OutputBlobUri;

                    if (string.IsNullOrEmpty(inputBlobUri) || string.IsNullOrEmpty(outputBlobUri))
                    {
                        _logger.LogWarning(
                            "Job {JobId} is missing blob URIs (input: {InputUri}, output: {OutputUri}), skipping resumption",
                            job.Id,
                            inputBlobUri,
                            outputBlobUri
                        );
                        continue;
                    }

                    // Try to acquire lock for this job first
                    var lockAcquired = await _client.IsJobLockedByAnotherInstanceAsync(
                        job.Id,
                        stoppingToken
                    );
                    if (lockAcquired)
                    {
                        _logger.LogDebug(
                            "Job {JobId} is already locked by another instance, skipping",
                            job.Id
                        );
                        continue;
                    }

                    // Get blob streams
                    await using var inputStream = await _blobStorageService.GetReadStreamAsync(
                        new Uri(inputBlobUri)
                    );
                    await using var outputStream = await _blobStorageService.GetWriteStreamAsync(
                        new Uri(outputBlobUri)
                    );

                    // Resume the job execution
                    var result = await _client.ResumeImportJobAsync(
                        job.Id,
                        inputStream,
                        outputStream,
                        stoppingToken
                    );

                    _logger.LogInformation(
                        "Successfully resumed job {JobId} with status {Status}",
                        job.Id,
                        result.Status
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to resume job {JobId}: {Error}",
                        job.Id,
                        ex.Message
                    );
                }
            }

            _logger.LogInformation("Job resumption service completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during job resumption service execution");
        }
    }
}
