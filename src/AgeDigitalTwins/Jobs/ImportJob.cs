using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs.Models;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Handles importing models, twins, and relationships from ND-JSON streams.
/// </summary>
internal class ImportJob
{
    private readonly AgeDigitalTwinsClient _client;
    private readonly IImportJobLogger _logger;
    private readonly ImportJobOptions _options;

    public ImportJob(
        AgeDigitalTwinsClient client,
        IImportJobLogger logger,
        ImportJobOptions options
    )
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Executes the import job from the provided input stream.
    /// </summary>
    /// <param name="inputStream">The ND-JSON input stream to process.</param>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import job result.</returns>
    public async Task<ImportJobResult> ExecuteAsync(
        Stream inputStream,
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        var result = new ImportJobResult
        {
            JobId = jobId,
            StartTime = DateTime.UtcNow,
            Status = ImportJobStatus.Started,
        };

        try
        {
            await _logger.LogInfoAsync(jobId, new { status = "Started" }, cancellationToken);
            result.Status = ImportJobStatus.Running;

            var lines = await ParseNdJsonAsync(inputStream, cancellationToken);

            // Process header
            var header = await ProcessHeaderAsync(lines, jobId, cancellationToken);

            // Process models
            await ProcessModelsAsync(lines, result, jobId, cancellationToken);

            // Process twins
            await ProcessTwinsAsync(lines, result, jobId, cancellationToken);

            // Process relationships
            await ProcessRelationshipsAsync(lines, result, jobId, cancellationToken);

            // Determine final status based on failures
            bool hasFailures =
                result.ModelsStats.Failed > 0
                || result.TwinsStats.Failed > 0
                || result.RelationshipsStats.Failed > 0;
            bool hasSuccesses =
                result.ModelsStats.Succeeded > 0
                || result.TwinsStats.Succeeded > 0
                || result.RelationshipsStats.Succeeded > 0;

            if (hasFailures && hasSuccesses)
            {
                result.Status = ImportJobStatus.PartiallySucceeded;
                await _logger.LogInfoAsync(
                    jobId,
                    new { status = "PartiallySucceeded" },
                    cancellationToken
                );
            }
            else if (hasSuccesses)
            {
                result.Status = ImportJobStatus.Succeeded;
                await _logger.LogInfoAsync(jobId, new { status = "Succeeded" }, cancellationToken);
            }
            else
            {
                result.Status = ImportJobStatus.Failed;
                await _logger.LogInfoAsync(
                    jobId,
                    new { status = "Failed", reason = "No items processed successfully" },
                    cancellationToken
                );
            }

            result.EndTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            result.Status = ImportJobStatus.Cancelled;
            result.EndTime = DateTime.UtcNow;
            await _logger.LogInfoAsync(jobId, new { status = "Cancelled" }, cancellationToken);
        }
        catch (Exception ex)
        {
            result.Status = ImportJobStatus.Failed;
            result.EndTime = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            await _logger.LogErrorAsync(
                jobId,
                new { status = "Failed", error = ex.Message },
                cancellationToken
            );
        }

        return result;
    }

    private async Task<List<ImportLine>> ParseNdJsonAsync(
        Stream inputStream,
        CancellationToken cancellationToken
    )
    {
        var lines = new List<ImportLine>();
        using var reader = new StreamReader(inputStream, Encoding.UTF8, leaveOpen: true);

        int lineNumber = 0;
        ImportSection? currentSection = null;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(line);

                // Check if this is a section header
                if (jsonElement.TryGetProperty("Section", out var sectionProperty))
                {
                    if (Enum.TryParse<ImportSection>(sectionProperty.GetString(), out var section))
                    {
                        currentSection = section;
                        continue;
                    }
                }

                lines.Add(
                    new ImportLine
                    {
                        Section = currentSection,
                        Content = jsonElement,
                        LineNumber = lineNumber,
                    }
                );
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON on line {lineNumber}: {ex.Message}", ex);
            }
        }

        return lines;
    }

    private async Task<ImportHeader?> ProcessHeaderAsync(
        List<ImportLine> lines,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        var headerLines = lines.Where(l => l.Section == ImportSection.Header).ToList();
        if (!headerLines.Any())
            return null;

        // Expect the first header line to contain metadata
        var headerData = headerLines.FirstOrDefault();
        if (headerData == null)
            return null;

        try
        {
            var header = JsonSerializer.Deserialize<ImportHeader>(headerData.Content.GetRawText());
            await _logger.LogInfoAsync(
                jobId,
                new { section = "Header", fileVersion = header?.FileVersion },
                cancellationToken
            );
            return header;
        }
        catch (JsonException ex)
        {
            await _logger.LogWarningAsync(
                jobId,
                new { section = "Header", warning = $"Failed to parse header: {ex.Message}" },
                cancellationToken
            );
            return null;
        }
    }

    private async Task ProcessModelsAsync(
        List<ImportLine> lines,
        ImportJobResult result,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        var modelLines = lines.Where(l => l.Section == ImportSection.Models).ToList();
        if (!modelLines.Any())
            return;

        await _logger.LogInfoAsync(
            jobId,
            new { section = "Models", status = "Started" },
            cancellationToken
        );

        var models = new List<string>();
        foreach (var line in modelLines)
        {
            try
            {
                models.Add(line.Content.GetRawText());
                result.ModelsStats.TotalProcessed++;
            }
            catch (Exception ex)
            {
                result.ModelsStats.Failed++;
                await _logger.LogErrorAsync(
                    jobId,
                    new
                    {
                        section = "Models",
                        line = line.LineNumber,
                        error = ex.Message,
                    },
                    cancellationToken
                );

                if (!_options.ContinueOnFailure)
                    throw;
            }
        }

        // Batch create models
        if (models.Any())
        {
            try
            {
                await _client.CreateModelsAsync(models, cancellationToken);
                result.ModelsStats.Succeeded = models.Count;
                await _logger.LogInfoAsync(
                    jobId,
                    new { section = "Models", status = "Succeeded" },
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                result.ModelsStats.Failed = models.Count;
                await _logger.LogErrorAsync(
                    jobId,
                    new
                    {
                        section = "Models",
                        status = "Failed",
                        error = ex.Message,
                    },
                    cancellationToken
                );

                if (!_options.ContinueOnFailure)
                    throw;
            }
        }
    }

    private async Task ProcessTwinsAsync(
        List<ImportLine> lines,
        ImportJobResult result,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        var twinLines = lines.Where(l => l.Section == ImportSection.Twins).ToList();
        if (!twinLines.Any())
            return;

        await _logger.LogInfoAsync(
            jobId,
            new { section = "Twins", status = "Started" },
            cancellationToken
        );

        foreach (var line in twinLines)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var twinJson = line.Content.GetRawText();
                var twinElement = JsonSerializer.Deserialize<JsonElement>(twinJson);

                if (!twinElement.TryGetProperty("$dtId", out var dtIdProperty))
                {
                    throw new ArgumentException("Twin missing required $dtId property");
                }

                var twinId = dtIdProperty.GetString();
                if (string.IsNullOrEmpty(twinId))
                {
                    throw new ArgumentException("Twin $dtId cannot be null or empty");
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken
                );
                timeoutCts.CancelAfter(_options.OperationTimeout);

                await _client.CreateOrReplaceDigitalTwinAsync<string>(
                    twinId,
                    twinJson,
                    cancellationToken: timeoutCts.Token
                );

                result.TwinsStats.TotalProcessed++;
                result.TwinsStats.Succeeded++;
            }
            catch (Exception ex)
            {
                result.TwinsStats.TotalProcessed++;
                result.TwinsStats.Failed++;
                await _logger.LogErrorAsync(
                    jobId,
                    new
                    {
                        section = "Twins",
                        line = line.LineNumber,
                        error = ex.Message,
                    },
                    cancellationToken
                );

                if (!_options.ContinueOnFailure)
                    throw;
            }
        }

        await _logger.LogInfoAsync(
            jobId,
            new { section = "Twins", status = "Succeeded" },
            cancellationToken
        );
    }

    private async Task ProcessRelationshipsAsync(
        List<ImportLine> lines,
        ImportJobResult result,
        string jobId,
        CancellationToken cancellationToken
    )
    {
        var relationshipLines = lines.Where(l => l.Section == ImportSection.Relationships).ToList();
        if (!relationshipLines.Any())
            return;

        await _logger.LogInfoAsync(
            jobId,
            new { section = "Relationships", status = "Started" },
            cancellationToken
        );

        foreach (var line in relationshipLines)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relationshipJson = line.Content.GetRawText();
                var relationshipElement = JsonSerializer.Deserialize<JsonElement>(relationshipJson);

                if (
                    !relationshipElement.TryGetProperty("$dtId", out var sourceIdProperty)
                    || !relationshipElement.TryGetProperty(
                        "$relationshipId",
                        out var relationshipIdProperty
                    )
                )
                {
                    throw new ArgumentException(
                        "Relationship missing required $dtId or $relationshipId property"
                    );
                }

                var sourceId = sourceIdProperty.GetString();
                var relationshipId = relationshipIdProperty.GetString();

                if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(relationshipId))
                {
                    throw new ArgumentException(
                        "Relationship $dtId and $relationshipId cannot be null or empty"
                    );
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken
                );
                timeoutCts.CancelAfter(_options.OperationTimeout);

                await _client.CreateOrReplaceRelationshipAsync<string>(
                    sourceId,
                    relationshipId,
                    relationshipJson,
                    cancellationToken: timeoutCts.Token
                );

                result.RelationshipsStats.TotalProcessed++;
                result.RelationshipsStats.Succeeded++;
            }
            catch (Exception ex)
            {
                result.RelationshipsStats.TotalProcessed++;
                result.RelationshipsStats.Failed++;
                await _logger.LogErrorAsync(
                    jobId,
                    new
                    {
                        section = "Relationships",
                        line = line.LineNumber,
                        error = ex.Message,
                    },
                    cancellationToken
                );

                if (!_options.ContinueOnFailure)
                    throw;
            }
        }

        await _logger.LogInfoAsync(
            jobId,
            new { section = "Relationships", status = "Succeeded" },
            cancellationToken
        );
    }
}
