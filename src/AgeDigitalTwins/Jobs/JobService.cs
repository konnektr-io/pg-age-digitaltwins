using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly TimeSpan _defaultJobRetention = TimeSpan.FromHours(24);
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public JobService(NpgsqlMultiHostDataSource dataSource, ILogger? logger = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _logger = logger;
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

        const string sql =
            @"
            INSERT INTO jobs.job_records (id, job_type, status, created_at, updated_at, purge_at, request_data)
            VALUES (@id, @jobType, @status, @createdAt, @updatedAt, @purgeAt, @requestData)
            RETURNING id, job_type, status, created_at, updated_at, finished_at, purge_at, request_data, result_data, error_data";

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

        const string sql =
            @"
            SELECT id, job_type, status, created_at, updated_at, finished_at, purge_at, request_data, result_data, error_data
            FROM jobs.job_records
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
            @"
            SELECT id, job_type, status, created_at, updated_at, finished_at, purge_at, request_data, result_data, error_data
            FROM jobs.job_records";

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
            @"
            UPDATE jobs.job_records
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

        const string sql = "DELETE FROM jobs.job_records WHERE id = @id";

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
    /// Initializes the database schema and table for job storage.
    /// </summary>
    private async Task InitializeSchemaAsync()
    {
        const string createSchemaSql = "CREATE SCHEMA IF NOT EXISTS jobs";
        const string createTableSql =
            @"
            CREATE TABLE IF NOT EXISTS jobs.job_records (
                id VARCHAR(255) PRIMARY KEY,
                job_type VARCHAR(100) NOT NULL,
                status VARCHAR(50) NOT NULL DEFAULT 'notstarted',
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                finished_at TIMESTAMP WITH TIME ZONE NULL,
                purge_at TIMESTAMP WITH TIME ZONE NOT NULL,
                request_data JSONB NULL,
                result_data JSONB NULL,
                error_data JSONB NULL
            )";

        const string createIndexSql =
            @"
            CREATE INDEX IF NOT EXISTS idx_job_records_job_type ON jobs.job_records(job_type);
            CREATE INDEX IF NOT EXISTS idx_job_records_status ON jobs.job_records(status);
            CREATE INDEX IF NOT EXISTS idx_job_records_created_at ON jobs.job_records(created_at);
            CREATE INDEX IF NOT EXISTS idx_job_records_purge_at ON jobs.job_records(purge_at)";

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
            RequestData = reader.IsDBNull(7)
                ? null
                : JsonSerializer.SerializeToDocument(reader.GetString(7)), // request_data
            ResultData = reader.IsDBNull(8)
                ? null
                : JsonSerializer.SerializeToDocument(reader.GetString(8)), // result_data
            ErrorData = reader.IsDBNull(9)
                ? null
                : JsonSerializer.SerializeToDocument(reader.GetString(9)), // error_data
        };
    }
}
