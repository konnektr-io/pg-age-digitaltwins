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
/// Unit tests for ImportJob input validation that don't require database connectivity.
/// </summary>
public class ImportJobValidationTests
{
    [Fact]
    public async Task ImportJob_WithEmptyStream_ShouldThrowArgumentException()
    {
        // Arrange
        using var inputStream = new MemoryStream();
        var logger = new ImportJobLogger();
        var options = new ImportJobOptions();
        
        // Create a mock client (we won't use it since validation should happen before any client operations)
        AgeDigitalTwinsClient? client = null;
        
        var importJob = new ImportJob(client!, logger, options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await importJob.ExecuteAsync(inputStream, "test-job-id")
        );

        Assert.Contains("Empty input stream", exception.Message);
    }

    [Fact]
    public async Task ImportJob_WithMissingHeader_ShouldThrowArgumentException()
    {
        // Arrange - Missing header section
        var invalidData =
            @"{""Section"": ""Models""}
{""@id"":""dtmi:com:test;1"",""@type"":""Interface"",""@context"":""dtmi:dtdl:context;2""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(invalidData));
        var logger = new ImportJobLogger();
        var options = new ImportJobOptions();
        
        // Create a mock client
        AgeDigitalTwinsClient? client = null;
        
        var importJob = new ImportJob(client!, logger, options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await importJob.ExecuteAsync(inputStream, "test-job-id")
        );

        Assert.Contains("First section must be 'Header'", exception.Message);
    }

    [Fact]
    public async Task ImportJob_WithInvalidFileVersion_ShouldThrowArgumentException()
    {
        // Arrange - Invalid file version
        var invalidData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""2.0.0"", ""author"": ""test"", ""organization"": ""contoso""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(invalidData));
        var logger = new ImportJobLogger();
        var options = new ImportJobOptions();
        
        // Create a mock client
        AgeDigitalTwinsClient? client = null;
        
        var importJob = new ImportJob(client!, logger, options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await importJob.ExecuteAsync(inputStream, "test-job-id")
        );

        Assert.Contains("Unsupported file version", exception.Message);
    }
}
