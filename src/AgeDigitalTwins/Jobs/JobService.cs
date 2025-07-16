using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Job service for managing jobs in the system using PostgreSQL storage.
/// </summary>
public class JobService
{
    private readonly NpgsqlMultiHostDataSource _dataSource;
    private readonly ILogger? _logger;
    private readonly string _schemaName;
    private readonly string _instanceId;
    private readonly TimeSpan _defaultJobRetention = TimeSpan.FromHours(24);
    private readonly TimeSpan _defaultLockDuration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromMinutes(1);
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public JobService(
        NpgsqlMultiHostDataSource dataSource,
        string graphName,
        ILogger? logger = null
    )
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _logger = logger;
        _schemaName = $"{graphName}_jobs";
        _instanceId =
            $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}";
        InitializeSchemaAsync().GetAwaiter().GetResult();
    }

    public async Task<JobRecord> CreateJobAsync<TRequest>(
        string jobId,
        string jobType,
        TRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));

        if (string.IsNullOrEmpty(jobType))
            throw new ArgumentException("Job type cannot be null or empty.", nameof(jobType));

        var now = DateTime.UtcNow;
        var purgeAt = now.Add(_defaultJobRetention);

        var requestJson =
            request != null ? JsonSerializer.SerializeToDocument(request, _jsonOptions) : null;

        var sql =
            $@"
            INSERT INTO {_schemaName}.job_records (id, job_type, status, created_at, updated_at, purge_at, request_data)
            VALUES (@id, @jobType, @status, @createdAt, @updatedAt, @purgeAt, @requestData)
            RETURNING id, job_type, status, created_at, updated_at, finished_at, purge_at, request_data, result_data, error_data, checkpoint_data";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", jobId);
        command.Parameters.AddWithValue("@jobType", jobType);
        command.Parameters.AddWithValue(
            "@status",
            JobStatus.NotStarted.ToString().ToLowerInvariant()
        );
        command.Parameters.AddWithValue("@createdAt", now);
        command.Parameters.AddWithValue("@updatedAt", now);
        command.Parameters.AddWithValue("@purgeAt", purgeAt);
        command.Parameters.AddWithValue("@requestData", (object?)requestJson ?? DBNull.Value);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapJobRecord(reader);
            }

            throw new InvalidOperationException("Failed to create job record.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505") // unique_violation
        {
            throw new InvalidOperationException($"Job with ID '{jobId}' already exists.", ex);
        }
    }

    public async Task<JobRecord?> GetJobAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return null;

        var sql =
            $@"
            SELECT id, job_type, status, created_at, updated_at, finished_at, purge_at, request_data, result_data, error_data, checkpoint_data
            FROM {_schemaName}.job_records
            WHERE id = @id";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", jobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapJobRecord(reader);
        }

        return null;
    }

    public async Task<IEnumerable<JobRecord>> ListJobsAsync(
        string? jobType = null,
        CancellationToken cancellationToken = default
    )
    {
        var sql =
            $@"
            SELECT id, job_type, status, created_at, updated_at, finished_at, purge_at, request_data, result_data, error_data, checkpoint_data
            FROM {_schemaName}.job_records";

        var whereClause = "";
        if (!string.IsNullOrEmpty(jobType))
        {
            whereClause = " WHERE job_type = @jobType";
        }

        sql += whereClause + " ORDER BY created_at DESC";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        if (!string.IsNullOrEmpty(jobType))
        {
            command.Parameters.AddWithValue("@jobType", jobType);
        }

        var jobs = new List<JobRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(MapJobRecord(reader));
        }

        return jobs;
    }

    public async Task<bool> UpdateJobStatusAsync(
        string jobId,
        JobStatus status,
        object? resultData = null,
        object? errorData = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var now = DateTime.UtcNow;
        var resultJson =
            resultData != null
                ? JsonSerializer.SerializeToDocument(resultData, _jsonOptions)
                : null;
        var errorJson =
            errorData != null ? JsonSerializer.SerializeToDocument(errorData, _jsonOptions) : null;

        var sql =
            $@"
            UPDATE {_schemaName}.job_records
            SET status = @status,
                updated_at = @updatedAt,
                finished_at = @finishedAt,
                result_data = COALESCE(@resultData, result_data),
                error_data = COALESCE(@errorData, error_data)
            WHERE id = @id";

        DateTime? finishedAt = null;
        if (
            status == JobStatus.Succeeded
            || status == JobStatus.Failed
            || status == JobStatus.Cancelled
        )
        {
            finishedAt = now;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@id", jobId);
        command.Parameters.AddWithValue("@status", status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("@updatedAt", now);
        command.Parameters.AddWithValue("@finishedAt", (object?)finishedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@resultData", (object?)resultJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@errorData", (object?)errorJson ?? DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    public async Task<bool> CancelJobAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        return await UpdateJobStatusAsync(
            jobId,
            JobStatus.Cancelled,
            cancellationToken: cancellationToken
        );
    }

    public async Task<bool> DeleteJobAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var sql = $"DELETE FROM {_schemaName}.job_records WHERE id = @id";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", jobId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    public async Task CompleteJobAsync<TResult>(
        string jobId,
        TResult result,
        CancellationToken cancellationToken = default
    )
    {
        var success = await UpdateJobStatusAsync(
            jobId,
            JobStatus.Succeeded,
            resultData: result,
            cancellationToken: cancellationToken
        );

        if (!success)
        {
            _logger?.LogWarning("Failed to complete job {JobId}", jobId);
            throw new InvalidOperationException($"Failed to complete job '{jobId}'.");
        }
    }

    public async Task FailJobAsync<TError>(
        string jobId,
        TError error,
        CancellationToken cancellationToken = default
    )
    {
        var success = await UpdateJobStatusAsync(
            jobId,
            JobStatus.Failed,
            errorData: error,
            cancellationToken: cancellationToken
        );

        if (!success)
        {
            _logger?.LogWarning("Failed to mark job {JobId} as failed", jobId);
            throw new InvalidOperationException($"Failed to mark job '{jobId}' as failed.");
        }
    }

    /// <summary>
    /// Saves a checkpoint for the specified job.
    /// </summary>
    /// <param name="checkpoint">The checkpoint data to save.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the checkpoint was saved successfully; otherwise false.</returns>
    public async Task<bool> SaveCheckpointAsync(
        ImportJobCheckpoint checkpoint,
        CancellationToken cancellationToken = default
    )
    {
        if (checkpoint == null || string.IsNullOrEmpty(checkpoint.JobId))
            return false;

        var checkpointJson = JsonSerializer.SerializeToDocument(checkpoint, _jsonOptions);

        var sql =
            $@"
            UPDATE {_schemaName}.job_records
            SET checkpoint_data = @checkpointData,
                updated_at = @updatedAt
            WHERE id = @jobId";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@jobId", checkpoint.JobId);
        command.Parameters.AddWithValue("@checkpointData", (object?)checkpointJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    /// <summary>
    /// Loads a checkpoint for the specified job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The checkpoint data if found; otherwise null.</returns>
    public async Task<ImportJobCheckpoint?> LoadCheckpointAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return null;

        var sql =
            $@"
            SELECT checkpoint_data
            FROM {_schemaName}.job_records
            WHERE id = @jobId";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@jobId", jobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                var checkpointJson = reader.GetString(0);
                return JsonSerializer.Deserialize<ImportJobCheckpoint>(
                    checkpointJson,
                    _jsonOptions
                );
            }
        }

        return null;
    }

    /// <summary>
    /// Clears the checkpoint for the specified job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the checkpoint was cleared successfully; otherwise false.</returns>
    public async Task<bool> ClearCheckpointAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var sql =
            $@"
            UPDATE {_schemaName}.job_records
            SET checkpoint_data = NULL,
                updated_at = @updatedAt
            WHERE id = @jobId";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@jobId", jobId);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    /// <summary>
    /// Initializes the database schema and table for job storage.
    /// </summary>
    private async Task InitializeSchemaAsync()
    {
        var createSchemaSql = $"CREATE SCHEMA IF NOT EXISTS {_schemaName}";
        var createTableSql =
            $@"
            CREATE TABLE IF NOT EXISTS {_schemaName}.job_records (
                id VARCHAR(255) PRIMARY KEY,
                job_type VARCHAR(100) NOT NULL,
                status VARCHAR(50) NOT NULL DEFAULT 'notstarted',
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                finished_at TIMESTAMP WITH TIME ZONE NULL,
                purge_at TIMESTAMP WITH TIME ZONE NOT NULL,
                request_data JSONB NULL,
                result_data JSONB NULL,
                error_data JSONB NULL,
                checkpoint_data JSONB NULL,
                lock_acquired_at TIMESTAMP WITH TIME ZONE NULL,
                lock_acquired_by VARCHAR(255) NULL,
                lock_lease_duration INTERVAL NOT NULL DEFAULT '5 minutes',
                lock_heartbeat_at TIMESTAMP WITH TIME ZONE NULL
            )";

        // Add checkpoint column if it doesn't exist (for existing installations)
        var addCheckpointColumnSql =
            $@"
            ALTER TABLE {_schemaName}.job_records 
            ADD COLUMN IF NOT EXISTS checkpoint_data JSONB NULL";

        // Add locking columns if they don't exist (for existing installations)
        var addLockingColumnsSql =
            $@"
            ALTER TABLE {_schemaName}.job_records 
            ADD COLUMN IF NOT EXISTS lock_acquired_at TIMESTAMP WITH TIME ZONE NULL,
            ADD COLUMN IF NOT EXISTS lock_acquired_by VARCHAR(255) NULL,
            ADD COLUMN IF NOT EXISTS lock_lease_duration INTERVAL NOT NULL DEFAULT '5 minutes',
            ADD COLUMN IF NOT EXISTS lock_heartbeat_at TIMESTAMP WITH TIME ZONE NULL";

        var createIndexSql =
            $@"
            CREATE INDEX IF NOT EXISTS idx_job_records_job_type ON {_schemaName}.job_records(job_type);
            CREATE INDEX IF NOT EXISTS idx_job_records_status ON {_schemaName}.job_records(status);
            CREATE INDEX IF NOT EXISTS idx_job_records_created_at ON {_schemaName}.job_records(created_at);
            CREATE INDEX IF NOT EXISTS idx_job_records_purge_at ON {_schemaName}.job_records(purge_at);
            CREATE INDEX IF NOT EXISTS idx_job_records_lock_acquired_by ON {_schemaName}.job_records(lock_acquired_by);
            CREATE INDEX IF NOT EXISTS idx_job_records_lock_acquired_at ON {_schemaName}.job_records(lock_acquired_at)";

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync();

            // Create schema
            await using (var command = new NpgsqlCommand(createSchemaSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            // Create table
            await using (var command = new NpgsqlCommand(createTableSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            // Add checkpoint column for existing installations
            await using (var command = new NpgsqlCommand(addCheckpointColumnSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            // Add locking columns for existing installations
            await using (var command = new NpgsqlCommand(addLockingColumnsSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            // Create indexes
            await using (var command = new NpgsqlCommand(createIndexSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            _logger?.LogDebug("Job storage schema initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize job storage schema");
            throw;
        }
    }

    private static JobRecord MapJobRecord(NpgsqlDataReader reader)
    {
        return new JobRecord
        {
            Id = reader.GetString(0), // id
            JobType = reader.GetString(1), // job_type
            Status = Enum.Parse<JobStatus>(reader.GetString(2), true), // status
            CreatedAt = reader.GetDateTime(3), // created_at
            UpdatedAt = reader.GetDateTime(4), // updated_at
            FinishedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5), // finished_at
            PurgeAt = reader.GetDateTime(6), // purge_at
            RequestData = reader.IsDBNull(7) ? null : JsonDocument.Parse(reader.GetString(7)), // request_data
            ResultData = reader.IsDBNull(8) ? null : JsonDocument.Parse(reader.GetString(8)), // result_data
            ErrorData = reader.IsDBNull(9) ? null : JsonDocument.Parse(reader.GetString(9)), // error_data
            // checkpoint_data is at index 10 but we don't need it in JobRecord - it's accessed via LoadCheckpointAsync
        };
    }

    /// <summary>
    /// Attempts to acquire a distributed lock for the specified job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="leaseDuration">The lease duration for the lock (optional, defaults to 5 minutes).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was acquired successfully; otherwise false.</returns>
    public async Task<bool> TryAcquireJobLockAsync(
        string jobId,
        TimeSpan? leaseDuration = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var lockDuration = leaseDuration ?? _defaultLockDuration;
        var now = DateTime.UtcNow;
        var lockExpiresAt = now.Add(lockDuration);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // First try to update existing job record
        var updateSql =
            $@"
            UPDATE {_schemaName}.job_records
            SET lock_acquired_at = @lockAcquiredAt,
                lock_acquired_by = @lockAcquiredBy,
                lock_lease_duration = @lockLeaseDuration,
                lock_heartbeat_at = @lockHeartbeatAt,
                updated_at = @updatedAt
            WHERE id = @jobId
            AND (
                lock_acquired_at IS NULL 
                OR (lock_acquired_at + lock_lease_duration) < @now
            )";

        await using var updateCommand = new NpgsqlCommand(updateSql, connection);

        updateCommand.Parameters.AddWithValue("@jobId", jobId);
        updateCommand.Parameters.AddWithValue("@lockAcquiredAt", now);
        updateCommand.Parameters.AddWithValue("@lockAcquiredBy", _instanceId);
        updateCommand.Parameters.AddWithValue("@lockLeaseDuration", lockDuration);
        updateCommand.Parameters.AddWithValue("@lockHeartbeatAt", now);
        updateCommand.Parameters.AddWithValue("@updatedAt", now);
        updateCommand.Parameters.AddWithValue("@now", now);

        var rowsAffected = await updateCommand.ExecuteNonQueryAsync(cancellationToken);

        // If update didn't affect any rows, try to insert a new job record
        if (rowsAffected == 0)
        {
            var insertSql =
                $@"
                INSERT INTO {_schemaName}.job_records (
                    id, job_type, status, created_at, updated_at, purge_at,
                    lock_acquired_at, lock_acquired_by, lock_lease_duration, lock_heartbeat_at
                ) VALUES (
                    @jobId, 'lock-only', 'pending', @now, @now, @purgeAt,
                    @lockAcquiredAt, @lockAcquiredBy, @lockLeaseDuration, @lockHeartbeatAt
                )
                ON CONFLICT (id) DO NOTHING";

            await using var insertCommand = new NpgsqlCommand(insertSql, connection);

            insertCommand.Parameters.AddWithValue("@jobId", jobId);
            insertCommand.Parameters.AddWithValue("@lockAcquiredAt", now);
            insertCommand.Parameters.AddWithValue("@lockAcquiredBy", _instanceId);
            insertCommand.Parameters.AddWithValue("@lockLeaseDuration", lockDuration);
            insertCommand.Parameters.AddWithValue("@lockHeartbeatAt", now);
            insertCommand.Parameters.AddWithValue("@now", now);
            insertCommand.Parameters.AddWithValue("@purgeAt", now.AddHours(24)); // Keep lock records for 24 hours

            var insertRows = await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            rowsAffected = insertRows;
        }

        var lockAcquired = rowsAffected > 0;

        if (lockAcquired)
        {
            _logger?.LogDebug(
                "Acquired lock for job {JobId} by instance {InstanceId}",
                jobId,
                _instanceId
            );
        }
        else
        {
            _logger?.LogWarning(
                "Failed to acquire lock for job {JobId} by instance {InstanceId}",
                jobId,
                _instanceId
            );
        }

        return lockAcquired;
    }

    /// <summary>
    /// Renews the heartbeat for the job lock to extend the lease.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the heartbeat was renewed successfully; otherwise false.</returns>
    public async Task<bool> RenewJobLockHeartbeatAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var now = DateTime.UtcNow;

        var sql =
            $@"
            UPDATE {_schemaName}.job_records
            SET lock_heartbeat_at = @lockHeartbeatAt,
                updated_at = @updatedAt
            WHERE id = @jobId
            AND lock_acquired_by = @lockAcquiredBy
            AND (lock_acquired_at + lock_lease_duration) > @now";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@jobId", jobId);
        command.Parameters.AddWithValue("@lockHeartbeatAt", now);
        command.Parameters.AddWithValue("@updatedAt", now);
        command.Parameters.AddWithValue("@lockAcquiredBy", _instanceId);
        command.Parameters.AddWithValue("@now", now);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        var heartbeatRenewed = rowsAffected > 0;

        if (heartbeatRenewed)
        {
            _logger?.LogDebug(
                "Renewed heartbeat for job {JobId} by instance {InstanceId}",
                jobId,
                _instanceId
            );
        }
        else
        {
            _logger?.LogWarning(
                "Failed to renew heartbeat for job {JobId} by instance {InstanceId} - lock may have expired",
                jobId,
                _instanceId
            );
        }

        return heartbeatRenewed;
    }

    /// <summary>
    /// Releases the distributed lock for the specified job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was released successfully; otherwise false.</returns>
    public async Task<bool> ReleaseJobLockAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var sql =
            $@"
            UPDATE {_schemaName}.job_records
            SET lock_acquired_at = NULL,
                lock_acquired_by = NULL,
                lock_heartbeat_at = NULL,
                updated_at = @updatedAt
            WHERE id = @jobId
            AND lock_acquired_by = @lockAcquiredBy";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@jobId", jobId);
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@lockAcquiredBy", _instanceId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        var lockReleased = rowsAffected > 0;

        if (lockReleased)
        {
            _logger?.LogDebug(
                "Released lock for job {JobId} by instance {InstanceId}",
                jobId,
                _instanceId
            );
        }
        else
        {
            _logger?.LogWarning(
                "Failed to release lock for job {JobId} by instance {InstanceId}",
                jobId,
                _instanceId
            );
        }

        return lockReleased;
    }

    /// <summary>
    /// Checks if the current instance owns the lock for the specified job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the current instance owns the lock and it hasn't expired; otherwise false.</returns>
    public async Task<bool> IsJobLockedByCurrentInstanceAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return false;

        var now = DateTime.UtcNow;

        var sql =
            $@"
            SELECT COUNT(*)
            FROM {_schemaName}.job_records
            WHERE id = @jobId
            AND lock_acquired_by = @lockAcquiredBy
            AND (lock_acquired_at + lock_lease_duration) > @now";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@jobId", jobId);
        command.Parameters.AddWithValue("@lockAcquiredBy", _instanceId);
        command.Parameters.AddWithValue("@now", now);

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }

    /// <summary>
    /// Gets information about the job lock.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Lock information if the job is locked; otherwise null.</returns>
    public async Task<JobLockInfo?> GetJobLockInfoAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(jobId))
            return null;

        var sql =
            $@"
            SELECT lock_acquired_at, lock_acquired_by, lock_lease_duration, lock_heartbeat_at
            FROM {_schemaName}.job_records
            WHERE id = @jobId
            AND lock_acquired_at IS NOT NULL";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@jobId", jobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var lockAcquiredAt = reader.GetDateTime(0);
            var lockAcquiredBy = reader.GetString(1);
            var lockLeaseDuration = reader.GetTimeSpan(2);
            var lockHeartbeatAt = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);

            return new JobLockInfo
            {
                JobId = jobId,
                LockAcquiredAt = lockAcquiredAt,
                LockAcquiredBy = lockAcquiredBy,
                LockLeaseDuration = lockLeaseDuration,
                LockHeartbeatAt = lockHeartbeatAt,
                LockExpiresAt = lockAcquiredAt.Add(lockLeaseDuration),
                IsExpired = DateTime.UtcNow > lockAcquiredAt.Add(lockLeaseDuration),
            };
        }

        return null;
    }

    /// <summary>
    /// Cleans up expired locks from all jobs.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of expired locks cleaned up.</returns>
    public async Task<int> CleanupExpiredLocksAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var sql =
            $@"
            UPDATE {_schemaName}.job_records
            SET lock_acquired_at = NULL,
                lock_acquired_by = NULL,
                lock_heartbeat_at = NULL,
                updated_at = @updatedAt
            WHERE lock_acquired_at IS NOT NULL
            AND (lock_acquired_at + lock_lease_duration) < @now";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("@updatedAt", now);
        command.Parameters.AddWithValue("@now", now);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

        if (rowsAffected > 0)
        {
            _logger?.LogInformation(
                "Cleaned up {ExpiredLockCount} expired job locks",
                rowsAffected
            );
        }

        return rowsAffected;
    }

    /// <summary>
    /// Gets all jobs that should be resumed on startup (jobs that are in progress but not locked by any instance).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of job records that need to be resumed.</returns>
    public async Task<List<JobRecord>> GetJobsToResumeAsync(
        CancellationToken cancellationToken = default
    )
    {
        string sql = $"""
            SELECT j.job_id, j.job_type, j.status, j.created_at, j.updated_at, j.request_data, j.result_data
            FROM {_schemaName}.jobs j
            LEFT JOIN {_schemaName}.job_locks jl ON j.job_id = jl.job_id AND jl.lock_acquired_at + jl.lock_lease_duration > NOW()
            WHERE j.status IN ('InProgress', 'Pending')
            AND jl.job_id IS NULL
            ORDER BY j.created_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        var jobs = new List<JobRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var job = new JobRecord
            {
                Id = reader.GetString(0), // job_id
                JobType = reader.GetString(1), // job_type
                Status = Enum.Parse<JobStatus>(reader.GetString(2)), // status
                CreatedAt = reader.GetDateTime(3), // created_at
                UpdatedAt = reader.GetDateTime(4), // updated_at
                RequestData = reader.IsDBNull(5) ? null : JsonDocument.Parse(reader.GetString(5)), // request_data
                ResultData = reader.IsDBNull(6)
                    ? null
                    : JsonDocument.Parse(
                        reader.GetString(6)
                    ) // result_data
                ,
            };
            jobs.Add(job);
        }

        _logger?.LogInformation("Found {JobCount} jobs to resume", jobs.Count);
        return jobs;
    }

    /// <summary>
    /// Resumes incomplete jobs by attempting to acquire locks and continue processing.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of job records that were successfully resumed.</returns>
    public async Task<List<JobRecord>> ResumeIncompleteJobsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var jobsToResume = await GetJobsToResumeAsync(cancellationToken);
        var resumedJobs = new List<JobRecord>();

        foreach (var job in jobsToResume)
        {
            try
            {
                // Try to acquire a lock for this job
                var lockAcquired = await TryAcquireJobLockAsync(
                    job.Id,
                    cancellationToken: cancellationToken
                );
                if (lockAcquired)
                {
                    _logger?.LogInformation(
                        "Acquired lock for job {JobId}, will resume execution",
                        job.Id
                    );

                    // Update the job status to indicate it's being resumed
                    await UpdateJobStatusAsync(
                        job.Id,
                        JobStatus.Running,
                        resultData: new { resumedAt = DateTime.UtcNow, resumedBy = _instanceId },
                        cancellationToken: cancellationToken
                    );

                    // Add to list of jobs that can be resumed
                    resumedJobs.Add(job);
                }
                else
                {
                    _logger?.LogDebug("Could not acquire lock for job {JobId}, skipping", job.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resuming job {JobId}", job.Id);
            }
        }

        _logger?.LogInformation(
            "Prepared {ResumedCount} out of {TotalJobs} incomplete jobs for resumption",
            resumedJobs.Count,
            jobsToResume.Count
        );
        return resumedJobs;
    }
}
