using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs.Models;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Interface for logging import job progress and events.
/// </summary>
public interface IImportJobLogger
{
    /// <summary>
    /// Logs an information message.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="details">The details object to log.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogInfoAsync(string jobId, object details, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="details">The details object to log.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogErrorAsync(string jobId, object details, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="details">The details object to log.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogWarningAsync(
        string jobId,
        object details,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Implementation of IImportJobLogger that writes to a stream.
/// </summary>
public class StreamImportJobLogger : IImportJobLogger, IDisposable
{
    private readonly Stream _outputStream;
    private readonly StreamWriter _writer;

    /// <summary>
    /// Initializes a new instance of the StreamImportJobLogger class.
    /// </summary>
    /// <param name="outputStream">The output stream to write log entries to.</param>
    public StreamImportJobLogger(Stream outputStream)
    {
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        _writer = new StreamWriter(_outputStream, leaveOpen: true) { AutoFlush = true };
    }

    /// <inheritdoc />
    public async Task LogInfoAsync(
        string jobId,
        object details,
        CancellationToken cancellationToken = default
    )
    {
        await LogAsync(jobId, "Info", details, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogErrorAsync(
        string jobId,
        object details,
        CancellationToken cancellationToken = default
    )
    {
        await LogAsync(jobId, "Error", details, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogWarningAsync(
        string jobId,
        object details,
        CancellationToken cancellationToken = default
    )
    {
        await LogAsync(jobId, "Warning", details, cancellationToken);
    }

    private async Task LogAsync(
        string jobId,
        string logType,
        object details,
        CancellationToken cancellationToken
    )
    {
        var logEntry = new ImportJobLogEntry
        {
            Timestamp = DateTime.UtcNow,
            JobId = jobId,
            LogType = logType,
            Details = details,
        };

        await _writer.WriteLineAsync(logEntry.ToJson());
    }

    /// <summary>
    /// Disposes the logger and underlying resources.
    /// </summary>
    public void Dispose()
    {
        _writer?.Dispose();
    }
}
