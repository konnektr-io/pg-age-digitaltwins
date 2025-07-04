using System;
using System.Text.Json;

namespace AgeDigitalTwins.Jobs.Models;

/// <summary>
/// Represents a log entry for import jobs.
/// </summary>
public class ImportJobLogEntry
{
    /// <summary>
    /// Gets or sets the timestamp of the log entry.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the job ID.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the job type.
    /// </summary>
    public string JobType { get; set; } = "Import";

    /// <summary>
    /// Gets or sets the log type.
    /// </summary>
    public string LogType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the details of the log entry.
    /// </summary>
    public object Details { get; set; } = new();

    /// <summary>
    /// Converts the log entry to a JSON string.
    /// </summary>
    /// <returns>JSON representation of the log entry.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );
    }
}
