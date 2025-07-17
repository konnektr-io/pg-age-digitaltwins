using System.Collections.Generic;

namespace AgeDigitalTwins.Models;

/// <summary>
/// Represents an error that occurred during an import job.
/// </summary>
public class ImportJobError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the inner error details.
    /// </summary>
    public ImportJobError? InnerError { get; set; }

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}
