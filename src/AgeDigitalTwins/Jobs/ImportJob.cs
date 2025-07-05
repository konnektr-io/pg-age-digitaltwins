using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs.Models;

namespace AgeDigitalTwins.Jobs;

/// <summary>
/// Static class for handling streaming import operations from ND-JSON format.
/// Processes data line-by-line without loading everything into memory.
/// </summary>
public static class StreamingImportJob
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    /// <summary>
    /// Executes a streaming import job that processes ND-JSON line by line.
    /// </summary>
    public static async Task<ImportJobResult> ExecuteAsync(
        AgeDigitalTwinsClient client,
        Stream inputStream,
        Stream outputStream,
        string jobId,
        ImportJobOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var result = new ImportJobResult
        {
            JobId = jobId,
            StartTime = DateTime.UtcNow,
            Status = ImportJobStatus.Running,
        };

        try
        {
            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new { status = "Started" },
                cancellationToken
            );

            await ProcessStreamAsync(
                client,
                inputStream,
                outputStream,
                jobId,
                options,
                result,
                cancellationToken
            );

            // Determine final status
            if (result.ErrorCount == 0)
            {
                result.Status = ImportJobStatus.Succeeded;
            }
            else if (
                result.ErrorCount > 0
                && (
                    result.ModelsCreated > 0
                    || result.TwinsCreated > 0
                    || result.RelationshipsCreated > 0
                )
            )
            {
                result.Status = ImportJobStatus.PartiallySucceeded;
            }
            else
            {
                result.Status = ImportJobStatus.Failed;
            }

            result.EndTime = DateTime.UtcNow;
            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new { status = result.Status.ToString() },
                cancellationToken
            );

            return result;
        }
        catch (Exception ex)
        {
            result.Status = ImportJobStatus.Failed;
            result.EndTime = DateTime.UtcNow;
            result.ErrorCount++;

            await LogAsync(
                outputStream,
                jobId,
                "Error",
                new { error = ex.Message },
                cancellationToken
            );

            if (!options.ContinueOnFailure)
            {
                throw;
            }

            return result;
        }
    }

    private static async Task ProcessStreamAsync(
        AgeDigitalTwinsClient client,
        Stream inputStream,
        Stream outputStream,
        string jobId,
        ImportJobOptions options,
        ImportJobResult result,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(inputStream, Encoding.UTF8);

        string? firstLine = await reader.ReadLineAsync();
        if (firstLine == null)
        {
            throw new ArgumentException("Empty input stream");
        }

        // Validate first line is header section
        var firstLineJson = JsonNode.Parse(firstLine);
        if (firstLineJson?["Section"]?.ToString() != "Header")
        {
            throw new ArgumentException("First section must be 'Header'");
        }

        // Read header data
        string? headerLine = await reader.ReadLineAsync();
        if (headerLine != null)
        {
            var headerData = JsonNode.Parse(headerLine);
            var fileVersion = headerData?["fileVersion"]?.ToString();
            if (fileVersion != "1.0.0")
            {
                throw new ArgumentException($"Unsupported file version: {fileVersion}");
            }
        }

        // Process remaining sections in streaming fashion
        CurrentSection currentSection = CurrentSection.None;
        List<string> allModels = new(); // Collect all models to process at once due to dependencies

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var jsonNode = JsonNode.Parse(line);

                // Check if this is a section header
                if (jsonNode?["Section"] != null)
                {
                    // Process all models when leaving the Models section
                    if (currentSection == CurrentSection.Models && allModels.Count > 0)
                    {
                        await ProcessAllModelsAsync(
                            client,
                            outputStream,
                            jobId,
                            allModels,
                            result,
                            options,
                            cancellationToken
                        );
                        allModels.Clear();
                    }

                    var sectionName = jsonNode["Section"]!.ToString();
                    currentSection = sectionName switch
                    {
                        "Models" => CurrentSection.Models,
                        "Twins" => CurrentSection.Twins,
                        "Relationships" => CurrentSection.Relationships,
                        _ => CurrentSection.None,
                    };

                    if (currentSection != CurrentSection.None)
                    {
                        await LogAsync(
                            outputStream,
                            jobId,
                            "Info",
                            new { section = sectionName, status = "Started" },
                            cancellationToken
                        );
                    }
                    continue;
                }

                // Process data based on current section
                switch (currentSection)
                {
                    case CurrentSection.Models:
                        // Collect all models to process at once due to potential dependencies
                        allModels.Add(line);
                        break;

                    case CurrentSection.Twins:
                        await ProcessTwinAsync(
                            client,
                            outputStream,
                            jobId,
                            line,
                            result,
                            options,
                            cancellationToken
                        );
                        break;

                    case CurrentSection.Relationships:
                        await ProcessRelationshipAsync(
                            client,
                            outputStream,
                            jobId,
                            line,
                            result,
                            options,
                            cancellationToken
                        );
                        break;
                }
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                await LogAsync(
                    outputStream,
                    jobId,
                    "Error",
                    new
                    {
                        section = currentSection.ToString(),
                        error = ex.Message,
                        line,
                    },
                    cancellationToken
                );

                if (!options.ContinueOnFailure)
                {
                    throw;
                }
            }
        }

        // Process any remaining models if we ended in the Models section
        if (currentSection == CurrentSection.Models && allModels.Count > 0)
        {
            await ProcessAllModelsAsync(
                client,
                outputStream,
                jobId,
                allModels,
                result,
                options,
                cancellationToken
            );
        }
    }

    private static async Task ProcessAllModelsAsync(
        AgeDigitalTwinsClient client,
        Stream outputStream,
        string jobId,
        List<string> allModels,
        ImportJobResult result,
        ImportJobOptions options,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    section = "Models",
                    status = "Processing",
                    totalModels = allModels.Count,
                },
                cancellationToken
            );

            var models = await client.CreateModelsAsync(allModels, cancellationToken);
            result.ModelsCreated += models.Count;

            await LogAsync(
                outputStream,
                jobId,
                "Info",
                new
                {
                    section = "Models",
                    status = "Succeeded",
                    modelsCreated = models.Count,
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            result.ErrorCount++;
            await LogAsync(
                outputStream,
                jobId,
                "Error",
                new { section = "Models", error = ex.Message },
                cancellationToken
            );

            if (!options.ContinueOnFailure)
            {
                throw;
            }
        }
    }

    private static async Task ProcessTwinAsync(
        AgeDigitalTwinsClient client,
        Stream outputStream,
        string jobId,
        string twinJson,
        ImportJobResult result,
        ImportJobOptions options,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var twinData = JsonNode.Parse(twinJson);
            var twinId = twinData?["$dtId"]?.ToString();

            if (string.IsNullOrEmpty(twinId))
            {
                throw new ArgumentException("Twin missing $dtId property");
            }

            await client.CreateOrReplaceDigitalTwinAsync(
                twinId,
                twinJson,
                cancellationToken: cancellationToken
            );
            result.TwinsCreated++;
        }
        catch (Exception ex)
        {
            result.ErrorCount++;
            await LogAsync(
                outputStream,
                jobId,
                "Error",
                new { section = "Twins", error = ex.Message },
                cancellationToken
            );

            if (!options.ContinueOnFailure)
            {
                throw;
            }
        }
    }

    private static async Task ProcessRelationshipAsync(
        AgeDigitalTwinsClient client,
        Stream outputStream,
        string jobId,
        string relationshipJson,
        ImportJobResult result,
        ImportJobOptions options,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var relData = JsonNode.Parse(relationshipJson);
            var sourceId = relData?["$dtId"]?.ToString();
            var relationshipId = relData?["$relationshipId"]?.ToString();

            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(relationshipId))
            {
                throw new ArgumentException(
                    "Relationship missing $dtId or $relationshipId property"
                );
            }

            await client.CreateOrReplaceRelationshipAsync(
                sourceId,
                relationshipId,
                relationshipJson,
                cancellationToken: cancellationToken
            );
            result.RelationshipsCreated++;
        }
        catch (Exception ex)
        {
            result.ErrorCount++;
            await LogAsync(
                outputStream,
                jobId,
                "Error",
                new { section = "Relationships", error = ex.Message },
                cancellationToken
            );

            if (!options.ContinueOnFailure)
            {
                throw;
            }
        }
    }

    private static async Task LogAsync(
        Stream outputStream,
        string jobId,
        string logType,
        object details,
        CancellationToken cancellationToken
    )
    {
        var logEntry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            jobId,
            jobType = "Import",
            logType,
            details,
        };

        var logJson = JsonSerializer.Serialize(logEntry, JsonOptions);
        var logBytes = Encoding.UTF8.GetBytes(logJson + Environment.NewLine);

        await outputStream.WriteAsync(logBytes, cancellationToken);
        await outputStream.FlushAsync(cancellationToken);
    }

    private enum CurrentSection
    {
        None,
        Models,
        Twins,
        Relationships,
    }
}
