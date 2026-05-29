// Copyright (c) Konnektr. All rights reserved.
// Licensed under the MIT License.

namespace AgeDigitalTwins;

/// <summary>
/// String constants for use in diagnostics and telemetry (ActivitySource, OpenTelemetry).
/// </summary>
internal static class DiagnosticConstants
{
    /// <summary>
    /// The name of the exception event in Activity tracing.
    /// </summary>
    public const string ActivityEventException = "Exception";

    /// <summary>
    /// The tag key for the exception type in Activity tags.
    /// </summary>
    public const string ActivityTagExceptionType = "exception.type";

    /// <summary>
    /// The tag key for the exception message in Activity tags.
    /// </summary>
    public const string ActivityTagExceptionMessage = "exception.message";

    /// <summary>
    /// The tag key for the exception stack trace in Activity tags.
    /// </summary>
    public const string ActivityTagExceptionStackTrace = "exception.stacktrace";
}
