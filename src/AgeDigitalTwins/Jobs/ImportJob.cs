using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Models;
using Npgsql;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Static class for handling streaming import operations from ND-JSON format.
/// Processes data line-by-line without loading everything into memory.
/// </summary>
public static class StreamingImportJob
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    /// <summary>
    /// Executes a streaming import job that processes ND-JSON line by line with checkpoint support.
    /// </summary>
    /// <param name="client">The AgeDigitalTwinsClient instance.</param>
    /// <param name="inputStream">The input stream containing ND-JSON data.</param>
    /// <param name="outputStream">The output stream for logging.</param>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="checkpoint">Optional checkpoint to resume from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The job result.</returns>
    public static async Task<JobRecord> ExecuteWithCheckpointAsync(
        AgeDigitalTwinsClient client,
        Stream inputStream,
        Stream outputStream,
        string jobId,
        ImportJobCheckpoint? checkpoint,
        CancellationToken cancellationToken = default
    )
    {
        var result = new JobRecord
        {
            Id = jobId,
            JobType = "import",
            CreatedDateTime = DateTime.UtcNow,
            LastActionDateTime = DateTime.UtcNow,
            Status = JobStatus.Running,
        };

        // Initialize or load checkpoint
        var currentCheckpoint = checkpoint ?? ImportJobCheckpoint.Create(jobId);

        // If resuming from checkpoint, restore progress
        if (checkpoint != null)
        {
            result.ModelsCreated = checkpoint.ModelsProcessed;
            result.TwinsCreated = checkpoint.TwinsProcessed;
            result.RelationshipsCreated = checkpoint.RelationshipsProcessed;
            result.ErrorCount = checkpoint.ErrorCount;

            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    status = "Resumed",
                    fromSection = checkpoint.CurrentSection.ToString(),
                    lineNumber = checkpoint.LineNumber,
                    progress = new
                    {
                        models = checkpoint.ModelsProcessed,
                        twins = checkpoint.TwinsProcessed,
                        relationships = checkpoint.RelationshipsProcessed,
                        errors = checkpoint.ErrorCount,
                    },
                },
                cancellationToken
            );
        }
        else
        {
            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new { status = "Started" },
                cancellationToken
            );
        }

        // Create a timer for periodic heartbeat updates
        using var heartbeatTimer = new Timer(
            async _ =>
            {
                try
                {
                    await client.JobService.RenewJobLockHeartbeatAsync(jobId, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log heartbeat failure but don't stop the job
                    await LogAsync(
                        outputStream,
                        jobId,
                        "Warning",
                        new { warning = $"Heartbeat renewal failed: {ex.Message}" },
                        cancellationToken
                    );
                }
            },
            null,
            TimeSpan.FromMinutes(1), // First heartbeat after 1 minute
            TimeSpan.FromMinutes(1) // Subsequent heartbeats every 1 minute
        );

        try
        {
            // Only validate stream header if starting from beginning
            if (checkpoint == null)
            {
                await ValidateStreamHeaderAsync(inputStream, cancellationToken);
            }

            // Open a single connection for the entire import job
            await using var connection = await client
                .GetDataSource()
                .OpenConnectionAsync(TargetSessionAttributes.ReadWrite, cancellationToken);

            await ProcessStreamWithCheckpointAsync(
                client,
                connection,
                inputStream,
                outputStream,
                jobId,
                result,
                currentCheckpoint,
                cancellationToken
            );

            // Determine final status
            DetermineFinalJobStatus(result);

            result.FinishedDateTime = DateTime.UtcNow;
            result.LastActionDateTime = DateTime.UtcNow;

            // Clear checkpoint on successful completion
            if (
                result.Status == JobStatus.Succeeded
                || result.Status == JobStatus.PartiallySucceeded
            )
            {
                await client.JobService.ClearCheckpointAsync(jobId, cancellationToken);
            }

            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new { status = result.Status.ToString() },
                cancellationToken
            );

            return result;
        }
        catch (ArgumentException)
        {
            // Always re-throw validation exceptions regardless of ContinueOnFailure
            throw;
        }
        catch (Exception ex)
        {
            // Don't change status to Failed here - let the final status determination logic handle it
            // The job should remain as Running until it's actually finished processing
            result.FinishedDateTime = DateTime.UtcNow;
            result.LastActionDateTime = DateTime.UtcNow;
            result.ErrorCount++;

            // Set the error information in the result
            result.Error = new ImportJobError
            {
                Code = ex.GetType().Name,
                Message = ex.Message,
                Details = new Dictionary<string, object>
                {
                    { "stackTrace", ex.StackTrace ?? string.Empty },
                    { "timestamp", DateTime.UtcNow.ToString("o") },
                },
            };

            // Determine final status based on results
            DetermineFinalJobStatus(result);

            await LogAsync(
                outputStream,
                jobId,
                "Error",
                new { error = ex.Message, finalStatus = result.Status.ToString() },
                cancellationToken
            );

            return result;
        }
        finally
        {
            // Dispose the heartbeat timer
            heartbeatTimer?.Dispose();
        }
    }

    /// <summary>
    /// Validates the stream header before processing.
    /// </summary>
    private static async Task ValidateStreamHeaderAsync(
        Stream inputStream,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(inputStream, Encoding.UTF8, leaveOpen: true);

        string? firstLine = await reader.ReadLineAsync(cancellationToken);
        if (firstLine == null)
        {
            throw new ArgumentException("Empty input stream");
        }

        // Validate first line is header section
        var firstLineJson = JsonNode.Parse(firstLine);
        if (firstLineJson?["Section"]?.ToString() != "Header")
        {
            throw new ArgumentException("First section must be 'Header'");
        }

        // Read header data
        string? headerLine = await reader.ReadLineAsync(cancellationToken);
        if (headerLine != null)
        {
            var headerData = JsonNode.Parse(headerLine);
            var fileVersion = headerData?["fileVersion"]?.ToString();
            if (fileVersion != "1.0.0")
            {
                throw new ArgumentException($"Unsupported file version: {fileVersion}");
            }
        }

        // Reset stream position to beginning for actual processing
        inputStream.Seek(0, SeekOrigin.Begin);
    }

    private static async Task ProcessStreamWithCheckpointAsync(
        AgeDigitalTwinsClient client,
        NpgsqlConnection connection,
        Stream inputStream,
        Stream outputStream,
        string jobId,
        JobRecord result,
        ImportJobCheckpoint checkpoint,
        CancellationToken cancellationToken
    )
    {
        using var reader = new PositionTrackingStreamReader(inputStream, leaveOpen: true);

        // If resuming from checkpoint, seek to the correct position
        if (checkpoint.LineNumber > 0)
        {
            await reader.SeekToLineAsync(checkpoint.LineNumber, cancellationToken);
        }
        else
        {
            // Skip header validation since it's already done
            // Read and skip header section line
            await reader.ReadLineAsync(cancellationToken); // Header section marker
            await reader.ReadLineAsync(cancellationToken); // Header data
            checkpoint.LineNumber = reader.LineNumber;
            checkpoint.CurrentSection = CurrentSection.Header;
        }

        // Process remaining sections in streaming fashion
        CurrentSection currentSection = checkpoint.CurrentSection;
        List<string> allModels = new(checkpoint.PendingModels); // Restore pending models from checkpoint

        // Batch processing for twins and relationships
        List<string> twinsBatch = new();
        List<string> relationshipsBatch = new();
        const int batchSize = 50;

        // Checkpoint save interval (save every 50 items)
        const int checkpointInterval = 50;
        int itemsSinceLastCheckpoint = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var jsonNode = JsonNode.Parse(line);

                // Check if this is a section header
                if (jsonNode?["Section"] != null)
                {
                    // Process any pending batches before switching sections
                    if (twinsBatch.Count > 0)
                    {
                        await ProcessTwinsBatchAsync(
                            client,
                            connection,
                            outputStream,
                            jobId,
                            twinsBatch,
                            result,
                            cancellationToken
                        );
                        twinsBatch.Clear();
                    }

                    if (relationshipsBatch.Count > 0)
                    {
                        await ProcessRelationshipsBatchAsync(
                            client,
                            connection,
                            outputStream,
                            jobId,
                            relationshipsBatch,
                            result,
                            cancellationToken
                        );
                        relationshipsBatch.Clear();
                    }

                    // Process all models when leaving the Models section
                    if (currentSection == CurrentSection.Models && allModels.Count > 0)
                    {
                        await ProcessAllModelsAsync(
                            client,
                            outputStream,
                            jobId,
                            allModels,
                            result,
                            cancellationToken
                        );
                        allModels.Clear();
                        checkpoint.ModelsCompleted = true;
                    }

                    var sectionName = jsonNode["Section"]!.ToString();
                    currentSection = sectionName switch
                    {
                        "Models" => CurrentSection.Models,
                        "Twins" => CurrentSection.Twins,
                        "Relationships" => CurrentSection.Relationships,
                        _ => CurrentSection.None,
                    };

                    // Update checkpoint with new section
                    checkpoint.CurrentSection = currentSection;
                    checkpoint.LineNumber = reader.LineNumber;
                    checkpoint.PendingModels.Clear();

                    // Save checkpoint at section boundaries
                    checkpoint.UpdateProgress(result);
                    await client.JobService.SaveCheckpointAsync(checkpoint, cancellationToken);

                    if (currentSection != CurrentSection.None)
                    {
                        await LogAsync(
                            outputStream,
                            jobId,
                            "Info",
                            new { section = sectionName, status = "Started" },
                            cancellationToken
                        );
                    }
                    continue;
                }

                // Skip processing if this section is already completed
                bool skipProcessing = false;
                switch (currentSection)
                {
                    case CurrentSection.Models when checkpoint.ModelsCompleted:
                        skipProcessing = true;
                        break;
                    case CurrentSection.Twins when checkpoint.TwinsCompleted:
                        skipProcessing = true;
                        break;
                    case CurrentSection.Relationships when checkpoint.RelationshipsCompleted:
                        skipProcessing = true;
                        break;
                }

                if (skipProcessing)
                {
                    continue;
                }

                // Process data based on current section
                switch (currentSection)
                {
                    case CurrentSection.Models:
                        // Collect all models to process at once due to potential dependencies
                        allModels.Add(line);
                        checkpoint.PendingModels = new List<string>(allModels);
                        break;

                    case CurrentSection.Twins:
                        // Add to batch instead of processing individually
                        twinsBatch.Add(line);

                        // Process batch when it reaches batchSize
                        if (twinsBatch.Count >= batchSize)
                        {
                            await ProcessTwinsBatchAsync(
                                client,
                                connection,
                                outputStream,
                                jobId,
                                twinsBatch,
                                result,
                                cancellationToken
                            );
                            twinsBatch.Clear();
                        }
                        break;

                    case CurrentSection.Relationships:
                        // Add to batch instead of processing individually
                        relationshipsBatch.Add(line);

                        // Process batch when it reaches batchSize
                        if (relationshipsBatch.Count >= batchSize)
                        {
                            await ProcessRelationshipsBatchAsync(
                                client,
                                connection,
                                outputStream,
                                jobId,
                                relationshipsBatch,
                                result,
                                cancellationToken
                            );
                            relationshipsBatch.Clear();
                        }
                        break;
                }

                // Update checkpoint periodically
                itemsSinceLastCheckpoint++;
                if (itemsSinceLastCheckpoint >= checkpointInterval)
                {
                    checkpoint.LineNumber = reader.LineNumber;
                    checkpoint.UpdateProgress(result);
                    await client.JobService.SaveCheckpointAsync(checkpoint, cancellationToken);
                    itemsSinceLastCheckpoint = 0;
                }
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                await LogAsync(
                    outputStream,
                    jobId,
                    "Error",
                    new
                    {
                        section = currentSection.ToString(),
                        error = ex.Message,
                        line,
                        lineNumber = reader.LineNumber,
                    },
                    cancellationToken
                );
            }
        }

        // Process any remaining models if we ended in the Models section
        if (currentSection == CurrentSection.Models && allModels.Count > 0)
        {
            await ProcessAllModelsAsync(
                client,
                outputStream,
                jobId,
                allModels,
                result,
                cancellationToken
            );
            checkpoint.ModelsCompleted = true;
        }

        // Process any remaining batches
        if (twinsBatch.Count > 0)
        {
            await ProcessTwinsBatchAsync(
                client,
                connection,
                outputStream,
                jobId,
                twinsBatch,
                result,
                cancellationToken
            );
        }

        if (relationshipsBatch.Count > 0)
        {
            await ProcessRelationshipsBatchAsync(
                client,
                connection,
                outputStream,
                jobId,
                relationshipsBatch,
                result,
                cancellationToken
            );
        }

        // Mark sections as completed
        switch (currentSection)
        {
            case CurrentSection.Twins:
                checkpoint.TwinsCompleted = true;
                break;
            case CurrentSection.Relationships:
                checkpoint.RelationshipsCompleted = true;
                break;
        }

        // Final checkpoint save
        checkpoint.UpdateProgress(result);
        await client.JobService.SaveCheckpointAsync(checkpoint, cancellationToken);
    }

    private static async Task ProcessAllModelsAsync(
        AgeDigitalTwinsClient client,
        Stream outputStream,
        string jobId,
        List<string> allModels,
        JobRecord result,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    section = "Models",
                    status = "Processing",
                    totalModels = allModels.Count,
                },
                cancellationToken
            );

            var models = await client.CreateModelsAsync(allModels, cancellationToken);
            result.ModelsCreated += models.Count;

            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    section = "Models",
                    status = "Succeeded",
                    modelsCreated = models.Count,
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            result.ErrorCount++;
            await LogAsync(
                outputStream,
                jobId,
                "Error",
                new { section = "Models", error = ex.Message },
                cancellationToken
            );
        }
    }

    private static async Task ProcessTwinsBatchAsync(
        AgeDigitalTwinsClient client,
        NpgsqlConnection connection,
        Stream outputStream,
        string jobId,
        List<string> twinsBatch,
        JobRecord result,
        CancellationToken cancellationToken
    )
    {
        if (twinsBatch.Count == 0)
            return;

        try
        {
            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    section = "Twins",
                    status = "Processing",
                    batchSize = twinsBatch.Count,
                },
                cancellationToken
            );

            var batchResult = await client.CreateOrReplaceDigitalTwinsInternalAsync(
                connection,
                twinsBatch,
                cancellationToken
            );

            var successCount = batchResult.Results.Count(r => r.IsSuccess);
            var errorCount = batchResult.Results.Count(r => !r.IsSuccess);

            result.TwinsCreated += successCount;
            result.ErrorCount += errorCount;

            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    section = "Twins",
                    status = "Completed",
                    successCount,
                    errorCount,
                },
                cancellationToken
            );

            // Log individual errors if any
            foreach (var error in batchResult.Results.Where(r => !r.IsSuccess))
            {
                await LogAsync(
                    outputStream,
                    jobId,
                    "Error",
                    new
                    {
                        section = "Twins",
                        digitalTwinId = error.DigitalTwinId,
                        error = error.ErrorMessage,
                    },
                    cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            result.ErrorCount += twinsBatch.Count;
            await LogAsync(
                outputStream,
                jobId,
                "Error",
                new
                {
                    section = "Twins",
                    error = ex.Message,
                    batchSize = twinsBatch.Count,
                },
                cancellationToken
            );
        }
    }

    private static async Task ProcessRelationshipsBatchAsync(
        AgeDigitalTwinsClient client,
        NpgsqlConnection connection,
        Stream outputStream,
        string jobId,
        List<string> relationshipsBatch,
        JobRecord result,
        CancellationToken cancellationToken
    )
    {
        if (relationshipsBatch.Count == 0)
            return;

        try
        {
            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    section = "Relationships",
                    status = "Processing",
                    batchSize = relationshipsBatch.Count,
                },
                cancellationToken
            );

            var batchResult = await client.CreateOrReplaceRelationshipsInternalAsync(
                connection,
                relationshipsBatch,
                cancellationToken
            );

            var successCount = batchResult.Results.Count(r => r.IsSuccess);
            var errorCount = batchResult.Results.Count(r => !r.IsSuccess);

            result.RelationshipsCreated += successCount;
            result.ErrorCount += errorCount;

            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    section = "Relationships",
                    status = "Completed",
                    successCount,
                    errorCount,
                },
                cancellationToken
            );

            // Log individual errors if any
            foreach (var error in batchResult.Results.Where(r => !r.IsSuccess))
            {
                await LogAsync(
                    outputStream,
                    jobId,
                    "Error",
                    new
                    {
                        section = "Relationships",
                        sourceId = error.SourceId,
                        relationshipId = error.RelationshipId,
                        error = error.ErrorMessage,
                    },
                    cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            result.ErrorCount += relationshipsBatch.Count;
            await LogAsync(
                outputStream,
                jobId,
                "Error",
                new
                {
                    section = "Relationships",
                    error = ex.Message,
                    batchSize = relationshipsBatch.Count,
                },
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Determines the final job status based on the results.
    /// </summary>
    /// <param name="result">The job result to update.</param>
    private static void DetermineFinalJobStatus(JobRecord result)
    {
        if (result.ErrorCount == 0)
        {
            result.Status = JobStatus.Succeeded;
        }
        else if (
            result.ErrorCount > 0
            && (
                result.ModelsCreated > 0
                || result.TwinsCreated > 0
                || result.RelationshipsCreated > 0
            )
        )
        {
            result.Status = JobStatus.PartiallySucceeded;
        }
        else
        {
            result.Status = JobStatus.Failed;
        }
    }

    private static async Task LogAsync(
        Stream outputStream,
        string jobId,
        string logType,
        object details,
        CancellationToken cancellationToken
    )
    {
        var logEntry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            jobId,
            jobType = "Import",
            logType,
            details,
        };

        var logJson = JsonSerializer.Serialize(logEntry, JsonOptions);
        var logBytes = Encoding.UTF8.GetBytes(logJson + Environment.NewLine);

        await outputStream.WriteAsync(logBytes, cancellationToken);
        await outputStream.FlushAsync(cancellationToken);
    }
}
