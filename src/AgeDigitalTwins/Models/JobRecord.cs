using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Job record for storing job information in the database.
/// </summary>
public class JobRecord
{
    /// <summary>
    /// Gets or sets the unique job identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the job type (e.g., "import", "delete").
    /// </summary>
    public required string JobType { get; set; }

    /// <summary>
    /// Gets or sets the current job status.
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the job creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the job completion timestamp (nullable).
    /// </summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// Gets or sets when the job will be purged from the system.
    /// </summary>
    public DateTimeOffset? PurgeAt { get; set; }

    /// <summary>
    /// Gets or sets job-specific request parameters as JSON.
    /// For import jobs: {"inputBlobUri": "...", "outputBlobUri": "..."}
    /// </summary>
    public JsonDocument? RequestData { get; set; }

    /// <summary>
    /// Gets or sets job-specific result data as JSON.
    /// For import jobs: statistics, blob URIs, etc.
    /// </summary>
    public JsonDocument? ResultData { get; set; }

    /// <summary>
    /// Gets or sets simple error information as JSON.
    /// Contains error code and message, not detailed logs.
    /// </summary>
    public JsonDocument? ErrorData { get; set; }

    /// <summary>
    /// Gets or sets checkpoint data for job resumption.
    /// Contains current progress for ongoing jobs.
    /// </summary>
    public JsonDocument? CheckpointData { get; set; }

    // Helper properties for Azure Digital Twins API compatibility
    /// <summary>
    /// Gets or sets the job creation time (Azure Digital Twins API compatibility).
    /// </summary>
    public DateTimeOffset CreatedDateTime
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    /// <summary>
    /// Gets or sets the last action time (Azure Digital Twins API compatibility).
    /// </summary>
    public DateTimeOffset LastActionDateTime
    {
        get => UpdatedAt;
        set => UpdatedAt = value;
    }

    /// <summary>
    /// Gets or sets the job completion time (Azure Digital Twins API compatibility).
    /// </summary>
    public DateTimeOffset? FinishedDateTime
    {
        get => FinishedAt;
        set => FinishedAt = value;
    }

    /// <summary>
    /// Gets or sets the time at which job will be purged by the service from the system (Azure Digital Twins API compatibility).
    /// </summary>
    public DateTimeOffset PurgeDateTime
    {
        get => PurgeAt ?? DateTimeOffset.MinValue;
        set => PurgeAt = value;
    }

    /// <summary>
    /// Gets or sets the path to the input Azure storage blob (for import jobs).
    /// </summary>
    public string? InputBlobUri
    {
        get => GetRequestProperty<string>("inputBlobUri");
        set => SetRequestProperty("inputBlobUri", value);
    }

    /// <summary>
    /// Gets or sets the path to the output Azure storage blob (for import jobs).
    /// </summary>
    public string? OutputBlobUri
    {
        get => GetRequestProperty<string>("outputBlobUri");
        set => SetRequestProperty("outputBlobUri", value);
    }

    /// <summary>
    /// Gets or sets details of the error(s) that occurred executing the import job.
    /// </summary>
    public ImportJobError? Error
    {
        get => GetErrorProperty<ImportJobError>("error");
        set => SetErrorProperty("error", value);
    }

    /// <summary>
    /// Gets or sets the number of models successfully created (import jobs).
    /// Shows current progress from checkpoint during execution, final results when completed.
    /// </summary>
    public int ModelsCreated
    {
        get
        {
            var checkpointValue = GetCheckpointProperty<int?>("modelsProcessed");
            if (checkpointValue.HasValue)
                return checkpointValue.Value;
            return GetResultProperty<int>("modelsCreated");
        }
        set => SetResultProperty("modelsCreated", value);
    }

    /// <summary>
    /// Gets or sets the number of twins successfully created (import jobs).
    /// Shows current progress from checkpoint during execution, final results when completed.
    /// </summary>
    public int TwinsCreated
    {
        get
        {
            var checkpointValue = GetCheckpointProperty<int?>("twinsProcessed");
            if (checkpointValue.HasValue)
                return checkpointValue.Value;
            return GetResultProperty<int>("twinsCreated");
        }
        set => SetResultProperty("twinsCreated", value);
    }

    /// <summary>
    /// Gets or sets the number of relationships successfully created (import jobs).
    /// Shows current progress from checkpoint during execution, final results when completed.
    /// </summary>
    public int RelationshipsCreated
    {
        get
        {
            var checkpointValue = GetCheckpointProperty<int?>("relationshipsProcessed");
            if (checkpointValue.HasValue)
                return checkpointValue.Value;
            return GetResultProperty<int>("relationshipsCreated");
        }
        set => SetResultProperty("relationshipsCreated", value);
    }

    /// <summary>
    /// Gets or sets the total number of errors encountered (import jobs).
    /// Shows current progress from checkpoint during execution, final results when completed.
    /// </summary>
    public int ErrorCount
    {
        get
        {
            var checkpointValue = GetCheckpointProperty<int?>("errorCount");
            if (checkpointValue.HasValue)
                return checkpointValue.Value;
            return GetResultProperty<int>("errorCount");
        }
        set => SetResultProperty("errorCount", value);
    }

    // Helper methods for JSON property access
    private T? GetRequestProperty<T>(string propertyName)
    {
        if (RequestData?.RootElement.TryGetProperty(propertyName, out var element) == true)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }

    private void SetRequestProperty<T>(string propertyName, T value)
    {
        Dictionary<string, object> dict;

        if (RequestData?.RootElement.ValueKind == JsonValueKind.Object)
        {
            try
            {
                dict =
                    JsonSerializer.Deserialize<Dictionary<string, object>>(
                        RequestData.RootElement.GetRawText()
                    ) ?? new Dictionary<string, object>();
            }
            catch
            {
                dict = new Dictionary<string, object>();
            }
        }
        else
        {
            dict = new Dictionary<string, object>();
        }

        if (value != null)
            dict[propertyName] = value;
        else
            dict.Remove(propertyName);

        RequestData = JsonSerializer.SerializeToDocument(dict);
    }

    private T? GetCheckpointProperty<T>(string propertyName)
    {
        if (CheckpointData?.RootElement.TryGetProperty(propertyName, out var element) == true)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }

    private T? GetResultProperty<T>(string propertyName)
    {
        if (ResultData?.RootElement.TryGetProperty(propertyName, out var element) == true)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }

    private void SetResultProperty<T>(string propertyName, T value)
    {
        Dictionary<string, object> dict;

        if (ResultData?.RootElement.ValueKind == JsonValueKind.Object)
        {
            try
            {
                dict =
                    JsonSerializer.Deserialize<Dictionary<string, object>>(
                        ResultData.RootElement.GetRawText()
                    ) ?? new Dictionary<string, object>();
            }
            catch
            {
                dict = new Dictionary<string, object>();
            }
        }
        else
        {
            dict = new Dictionary<string, object>();
        }

        if (value != null)
            dict[propertyName] = value;
        else
            dict.Remove(propertyName);

        ResultData = JsonSerializer.SerializeToDocument(dict);
    }

    private T? GetErrorProperty<T>(string propertyName)
    {
        if (ErrorData?.RootElement.TryGetProperty(propertyName, out var element) == true)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        return default;
    }

    private void SetErrorProperty<T>(string propertyName, T value)
    {
        var json = ErrorData?.RootElement.Clone() ?? new JsonElement();
        var dict =
            JsonSerializer.Deserialize<Dictionary<string, object>>(json.GetRawText() ?? "{}")
            ?? new Dictionary<string, object>();

        if (value != null)
            dict[propertyName] = value;
        else
            dict.Remove(propertyName);

        ErrorData = JsonSerializer.SerializeToDocument(dict);
    }
}
