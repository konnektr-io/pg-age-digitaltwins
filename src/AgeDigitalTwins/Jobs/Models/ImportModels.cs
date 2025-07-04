using System;
using System.Text.Json;

namespace AgeDigitalTwins.Jobs.Models;

/// <summary>
/// Represents the sections that can appear in an ND-JSON import file.
/// </summary>
public enum ImportSection
{
    /// <summary>
    /// Header section containing metadata.
    /// </summary>
    Header,

    /// <summary>
    /// Models section containing DTDL model definitions.
    /// </summary>
    Models,

    /// <summary>
    /// Twins section containing digital twin instances.
    /// </summary>
    Twins,

    /// <summary>
    /// Relationships section containing relationships between twins.
    /// </summary>
    Relationships,
}

/// <summary>
/// Represents a parsed line from an ND-JSON import file.
/// </summary>
public class ImportLine
{
    /// <summary>
    /// Gets or sets the section this line belongs to.
    /// </summary>
    public ImportSection? Section { get; set; }

    /// <summary>
    /// Gets or sets the raw JSON content of the line.
    /// </summary>
    public JsonElement Content { get; set; }

    /// <summary>
    /// Gets or sets the line number in the file.
    /// </summary>
    public int LineNumber { get; set; }
}

/// <summary>
/// Represents header metadata from an import file.
/// </summary>
public class ImportHeader
{
    /// <summary>
    /// Gets or sets the file version.
    /// </summary>
    public string FileVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author of the file.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the organization.
    /// </summary>
    public string? Organization { get; set; }
}
