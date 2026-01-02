using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Static class for handling delete operations that remove all relationships, twins, and models.
/// Processes deletions in the correct order with checkpoint support.
/// </summary>
public static class DeleteJob
{
    /// <summary>
    /// Executes a delete job that removes all relationships, twins, and models with checkpoint support.
    /// </summary>
    /// <param name="client">The AgeDigitalTwinsClient instance.</param>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="checkpoint">Optional checkpoint to resume from.</param>
    /// <param name="options">Delete job options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The job result.</returns>
    public static async Task<JobRecord> ExecuteWithCheckpointAsync(
        AgeDigitalTwinsClient client,
        string jobId,
        DeleteJobCheckpoint? checkpoint,
        DeleteJobOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new DeleteJobOptions()
        {
            BatchSize = client.DefaultBatchSize,
            CheckpointInterval = client.DefaultCheckpointInterval,
            HeartbeatInterval = client.DefaultHeartbeatInterval,
        };
        checkpoint ??= DeleteJobCheckpoint.Create(jobId);

        var result = new JobRecord
        {
            Id = jobId,
            JobType = "delete",
            Status = JobStatus.Running,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RelationshipsDeleted = checkpoint.RelationshipsDeleted,
            TwinsDeleted = checkpoint.TwinsDeleted,
            ModelsDeleted = checkpoint.ModelsDeleted,
            ErrorCount = checkpoint.ErrorCount,
        };

        // Create a cancellation token source that can be triggered by database status
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var combinedToken = combinedCts.Token;

        // Create a timer for periodic heartbeat updates and cancellation checks
        using var heartbeatTimer = new Timer(
            async _ =>
            {
                try
                {
                    await client.JobService.RenewJobLockHeartbeatAsync(
                        jobId,
                        CancellationToken.None
                    );

                    // Check if cancellation has been requested in the database
                    var currentJob = await client.JobService.GetJobAsync(
                        jobId,
                        CancellationToken.None
                    );
                    if (currentJob?.Status == JobStatus.Cancelling)
                    {
                        combinedCts.Cancel();
                    }
                }
                catch
                {
                    // Ignore heartbeat errors - they're handled elsewhere
                }
            },
            null,
            options.HeartbeatInterval,
            options.HeartbeatInterval
        );

        try
        {
            await ProcessDeleteWithCheckpointAsync(
                client,
                jobId,
                result,
                checkpoint,
                options,
                combinedToken
            );

            // Final status determination
            if (result.ErrorCount > 0 && !options.ContinueOnFailure)
            {
                result.Status = JobStatus.Failed;
            }
            else
            {
                result.Status = JobStatus.Succeeded;
            }

            result.FinishedAt = DateTime.UtcNow;
            result.UpdatedAt = DateTime.UtcNow;

            // Clear checkpoint on successful completion
            await client.JobService.ClearCheckpointAsync(jobId, cancellationToken);

            // Update final job status
            await client.JobService.UpdateJobStatusAsync(
                jobId,
                result.Status,
                resultData: new
                {
                    relationshipsDeleted = result.RelationshipsDeleted,
                    twinsDeleted = result.TwinsDeleted,
                    modelsDeleted = result.ModelsDeleted,
                    errorCount = result.ErrorCount,
                    completedAt = DateTime.UtcNow,
                },
                cancellationToken: cancellationToken
            );

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.Status = JobStatus.Cancelled;
            result.FinishedAt = DateTime.UtcNow;
            result.UpdatedAt = DateTime.UtcNow;

            await client.JobService.UpdateJobStatusAsync(
                jobId,
                result.Status,
                resultData: new
                {
                    relationshipsDeleted = result.RelationshipsDeleted,
                    twinsDeleted = result.TwinsDeleted,
                    modelsDeleted = result.ModelsDeleted,
                    errorCount = result.ErrorCount,
                    cancelledAt = DateTime.UtcNow,
                },
                cancellationToken: CancellationToken.None
            );

            return result;
        }
        catch (Exception ex)
        {
            result.Status = JobStatus.Failed;
            result.FinishedAt = DateTime.UtcNow;
            result.UpdatedAt = DateTime.UtcNow;
            result.Error = new JobError
            {
                Code = ex.GetType().Name,
                Message = ex.Message,
                Details = new Dictionary<string, object>
                {
                    { "stackTrace", ex.StackTrace ?? string.Empty },
                    { "timestamp", DateTime.UtcNow.ToString("o") },
                },
            };

            await client.JobService.UpdateJobStatusAsync(
                jobId,
                result.Status,
                resultData: new
                {
                    relationshipsDeleted = result.RelationshipsDeleted,
                    twinsDeleted = result.TwinsDeleted,
                    modelsDeleted = result.ModelsDeleted,
                    errorCount = result.ErrorCount,
                    failedAt = DateTime.UtcNow,
                },
                errorData: result.Error,
                cancellationToken: CancellationToken.None
            );

            return result;
        }
        finally
        {
            combinedCts?.Dispose();
        }
    }

    private static async Task ProcessDeleteWithCheckpointAsync(
        AgeDigitalTwinsClient client,
        string jobId,
        JobRecord result,
        DeleteJobCheckpoint checkpoint,
        DeleteJobOptions options,
        CancellationToken cancellationToken
    )
    {
        // Phase 1: Delete all relationships
        if (
            !checkpoint.RelationshipsCompleted
            && checkpoint.CurrentSection == DeleteSection.Relationships
        )
        {
            await DeleteAllRelationshipsAsync(
                client,
                result,
                checkpoint,
                options,
                cancellationToken
            );
            checkpoint.RelationshipsCompleted = true;
            checkpoint.CurrentSection = DeleteSection.Twins;
            await SaveCheckpointAsync(client, checkpoint, cancellationToken);
        }

        // Phase 2: Delete all twins
        if (!checkpoint.TwinsCompleted && checkpoint.CurrentSection == DeleteSection.Twins)
        {
            await DeleteAllTwinsAsync(client, result, checkpoint, options, cancellationToken);
            checkpoint.TwinsCompleted = true;
            checkpoint.CurrentSection = DeleteSection.Models;
            await SaveCheckpointAsync(client, checkpoint, cancellationToken);
        }

        // Phase 3: Delete all models
        if (!checkpoint.ModelsCompleted && checkpoint.CurrentSection == DeleteSection.Models)
        {
            await DeleteAllModelsAsync(client, result, checkpoint, options, cancellationToken);
            checkpoint.ModelsCompleted = true;
            checkpoint.CurrentSection = DeleteSection.Completed;
            await SaveCheckpointAsync(client, checkpoint, cancellationToken);
        }
    }

    private static async Task DeleteAllRelationshipsAsync(
        AgeDigitalTwinsClient client,
        JobRecord result,
        DeleteJobCheckpoint checkpoint,
        DeleteJobOptions options,
        CancellationToken cancellationToken
    )
    {
        int batchCount = 0;
        int relationshipsDeletedInBatch;

        do
        {
            relationshipsDeletedInBatch = await DeleteRelationshipsBatchAsync(
                client,
                options.BatchSize,
                cancellationToken
            );

            result.RelationshipsDeleted += relationshipsDeletedInBatch;
            if (batchCount % (Math.Max(1, options.CheckpointInterval / options.BatchSize) + 1) == 0)
                batchCount++;

            // Save checkpoint every CheckpointInterval batches
            if (batchCount % (Math.Max(1, options.CheckpointInterval / options.BatchSize) + 1) == 0)
            {
                await SaveCheckpointAsync(client, checkpoint, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
        } while (relationshipsDeletedInBatch > 0);
    }

    private static async Task DeleteAllTwinsAsync(
        AgeDigitalTwinsClient client,
        JobRecord result,
        DeleteJobCheckpoint checkpoint,
        DeleteJobOptions options,
        CancellationToken cancellationToken
    )
    {
        int batchCount = 0;
        int twinsDeletedInBatch;

        do
        {
            twinsDeletedInBatch = await DeleteTwinsBatchAsync(
                client,
                options.BatchSize,
                cancellationToken
            );

            result.TwinsDeleted += twinsDeletedInBatch;
            if (batchCount % (Math.Max(1, options.CheckpointInterval / options.BatchSize) + 1) == 0)
                batchCount++;

            // Save checkpoint every CheckpointInterval batches
            if (batchCount % (Math.Max(1, options.CheckpointInterval / options.BatchSize) + 1) == 0)
            {
                await SaveCheckpointAsync(client, checkpoint, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
        } while (twinsDeletedInBatch > 0);
    }

    private static async Task DeleteAllModelsAsync(
        AgeDigitalTwinsClient client,
        JobRecord result,
        DeleteJobCheckpoint checkpoint,
        DeleteJobOptions options,
        CancellationToken cancellationToken
    )
    {
        result.ModelsDeleted = await client.DeleteAllModelsAsync(cancellationToken);
        checkpoint.ModelsDeleted = result.ModelsDeleted;
    }

    private static async Task<int> DeleteRelationshipsBatchAsync(
        AgeDigitalTwinsClient client,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        // Query for a batch of relationships between twins and delete them
        var query = $"MATCH (:Twin)-[r]->(:Twin) RETURN r LIMIT {batchSize}";
        int deletedCount = 0;

        await foreach (
            var relationship in client.QueryAsync<JsonDocument>(query, cancellationToken)
        )
        {
            try
            {
                var relationshipElement = relationship?.RootElement;
                if (relationshipElement == null)
                    continue;

                string? sourceId = null;
                string? relationshipId = null;

                // Try direct property access
                if (relationshipElement.Value.TryGetProperty("$sourceId", out var sourceIdProp))
                {
                    sourceId = sourceIdProp.GetString();
                }

                if (relationshipElement.Value.TryGetProperty("$relationshipId", out var relIdProp))
                {
                    relationshipId = relIdProp.GetString();
                }

                // If direct access fails, try nested structure
                if (
                    sourceId == null
                    && relationshipElement.Value.TryGetProperty("r", out var rProp)
                )
                {
                    if (rProp.TryGetProperty("$sourceId", out var nestedSourceIdProp))
                    {
                        sourceId = nestedSourceIdProp.GetString();
                    }
                    if (rProp.TryGetProperty("$relationshipId", out var nestedRelIdProp))
                    {
                        relationshipId = nestedRelIdProp.GetString();
                    }
                }

                if (!string.IsNullOrEmpty(sourceId) && !string.IsNullOrEmpty(relationshipId))
                {
                    await client.DeleteRelationshipAsync(
                        sourceId,
                        relationshipId,
                        cancellationToken
                    );
                    deletedCount++;
                }
            }
            catch (RelationshipNotFoundException)
            {
                // Relationship already deleted, continue
            }
            catch (Exception)
            {
                // Log error but continue if ContinueOnFailure is true
                throw; // For now, always throw to be safe
            }
        }

        return deletedCount;
    }

    private static async Task<int> DeleteTwinsBatchAsync(
        AgeDigitalTwinsClient client,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        // Query for a batch of twins and delete them
        var query = $"MATCH (t:Twin) RETURN t.`$dtId` as dtId LIMIT {batchSize}";
        int deletedCount = 0;

        await foreach (var twin in client.QueryAsync<JsonDocument>(query, cancellationToken))
        {
            try
            {
                var twinId = twin?.RootElement.GetProperty("dtId").GetString();
                if (!string.IsNullOrEmpty(twinId))
                {
                    await client.DeleteDigitalTwinAsync(twinId, cancellationToken);
                    deletedCount++;
                }
            }
            catch (DigitalTwinNotFoundException)
            {
                // Twin already deleted, continue
            }
            catch (Exception)
            {
                // Log error but continue if ContinueOnFailure is true
                throw; // For now, always throw to be safe
            }
        }

        return deletedCount;
    }

    private static async Task SaveCheckpointAsync(
        AgeDigitalTwinsClient client,
        DeleteJobCheckpoint checkpoint,
        CancellationToken cancellationToken
    )
    {
        checkpoint.LastUpdated = DateTime.UtcNow;
        await client.JobService.SaveCheckpointAsync(checkpoint, cancellationToken);
    }
}
