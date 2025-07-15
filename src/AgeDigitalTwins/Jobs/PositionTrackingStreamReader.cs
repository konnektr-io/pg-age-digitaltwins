using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// A wrapper around StreamReader that tracks line numbers and stream positions
/// for checkpoint and resume functionality.
/// </summary>
public class PositionTrackingStreamReader : IDisposable
{
    private readonly StreamReader _reader;
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _disposed;

    /// <summary>
    /// Gets the current line number (1-based).
    /// </summary>
    public int LineNumber { get; private set; }

    /// <summary>
    /// Gets the current stream position.
    /// </summary>
    public long Position => _stream.Position;

    /// <summary>
    /// Initializes a new instance of the PositionTrackingStreamReader class.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposing.</param>
    public PositionTrackingStreamReader(
        Stream stream,
        Encoding? encoding = null,
        bool leaveOpen = false
    )
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _reader = new StreamReader(stream, encoding ?? Encoding.UTF8, leaveOpen: true);
        _leaveOpen = leaveOpen;
        LineNumber = 0;
    }

    /// <summary>
    /// Reads a line from the stream and updates the line number.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The line read from the stream, or null if end of stream.</returns>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var line = await _reader.ReadLineAsync(cancellationToken);
        if (line != null)
        {
            LineNumber++;
        }
        return line;
    }

    /// <summary>
    /// Seeks to a specific line number by reading from the beginning of the stream.
    /// This is an expensive operation as it requires reading through the entire stream.
    /// </summary>
    /// <param name="targetLineNumber">The target line number (1-based).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the target line was reached; false if end of stream was reached first.</returns>
    public async Task<bool> SeekToLineAsync(
        int targetLineNumber,
        CancellationToken cancellationToken = default
    )
    {
        if (targetLineNumber < 1)
            throw new ArgumentException(
                "Line number must be 1 or greater.",
                nameof(targetLineNumber)
            );

        // Reset stream to beginning
        _stream.Seek(0, SeekOrigin.Begin);
        _reader.DiscardBufferedData();
        LineNumber = 0;

        // Read lines until we reach the target line
        while (LineNumber < targetLineNumber)
        {
            var line = await ReadLineAsync(cancellationToken);
            if (line == null)
            {
                // End of stream reached before target line
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Skips a specified number of lines from the current position.
    /// </summary>
    /// <param name="linesToSkip">The number of lines to skip.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of lines actually skipped.</returns>
    public async Task<int> SkipLinesAsync(
        int linesToSkip,
        CancellationToken cancellationToken = default
    )
    {
        if (linesToSkip < 0)
            throw new ArgumentException("Lines to skip must be non-negative.", nameof(linesToSkip));

        int skipped = 0;
        for (int i = 0; i < linesToSkip; i++)
        {
            var line = await ReadLineAsync(cancellationToken);
            if (line == null)
            {
                // End of stream reached
                break;
            }
            skipped++;
        }

        return skipped;
    }

    /// <summary>
    /// Resets the stream to the beginning and resets the line counter.
    /// </summary>
    public void Reset()
    {
        _stream.Seek(0, SeekOrigin.Begin);
        _reader.DiscardBufferedData();
        LineNumber = 0;
    }

    /// <summary>
    /// Disposes the stream reader and optionally the underlying stream.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _reader?.Dispose();

        if (!_leaveOpen)
        {
            _stream?.Dispose();
        }

        _disposed = true;
    }
}
