using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Jobs.Models;
using Xunit;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Unit tests for StreamingImportJob input validation that don't require database connectivity.
/// </summary>
public class ImportJobValidationTests
{
    [Fact]
    public async Task StreamingImportJob_WithEmptyStream_ShouldThrowArgumentException()
    {
        // Arrange
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();
        var options = new ImportJobOptions();

        // Create a mock client (we won't use it since validation should happen before any client operations)
        AgeDigitalTwinsClient? client = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await StreamingImportJob.ExecuteAsync(
                    client!,
                    inputStream,
                    outputStream,
                    "test-job-id",
                    options
                )
        );

        Assert.Contains("Empty input stream", exception.Message);
    }

    [Fact]
    public async Task StreamingImportJob_WithMissingHeader_ShouldThrowArgumentException()
    {
        // Arrange - Missing header section
        var invalidData =
            @"{""Section"": ""Models""}
{""@id"":""dtmi:com:test;1"",""@type"":""Interface"",""@context"":""dtmi:dtdl:context;2""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(invalidData));
        using var outputStream = new MemoryStream();
        var options = new ImportJobOptions();

        // Create a mock client
        AgeDigitalTwinsClient? client = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await StreamingImportJob.ExecuteAsync(
                    client!,
                    inputStream,
                    outputStream,
                    "test-job-id",
                    options
                )
        );

        Assert.Contains("First section must be 'Header'", exception.Message);
    }

    [Fact]
    public async Task StreamingImportJob_WithInvalidFileVersion_ShouldThrowArgumentException()
    {
        // Arrange - Invalid file version
        var invalidData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""2.0.0"", ""author"": ""test"", ""organization"": ""contoso""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(invalidData));
        using var outputStream = new MemoryStream();
        var options = new ImportJobOptions();

        // Create a mock client
        AgeDigitalTwinsClient? client = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await StreamingImportJob.ExecuteAsync(
                    client!,
                    inputStream,
                    outputStream,
                    "test-job-id",
                    options
                )
        );

        Assert.Contains("Unsupported file version", exception.Message);
    }
}
