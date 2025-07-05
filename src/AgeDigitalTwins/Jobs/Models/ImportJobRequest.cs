using System;

namespace AgeDigitalTwins.Jobs.Models;

/// <summary>
/// Request model for creating an import job.
/// </summary>
public class ImportJobRequest
{
    /// <summary>
    /// Gets or sets the path to the input Azure storage blob or stream that contains file(s) describing the operations to perform in the job.
    /// </summary>
    public string? InputBlobUri { get; set; }

    /// <summary>
    /// Gets or sets the path to the output Azure storage blob or stream that will contain the errors and progress logs of import job.
    /// </summary>
    public string? OutputBlobUri { get; set; }

    /// <summary>
    /// Gets or sets the import job options. If null, default options will be used.
    /// </summary>
    public ImportJobOptions? Options { get; set; }
}
