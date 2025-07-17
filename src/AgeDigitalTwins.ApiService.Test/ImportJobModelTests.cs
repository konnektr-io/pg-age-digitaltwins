using System;
using System.Text.Json;
using AgeDigitalTwins.ApiService.Models;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test;

public class ImportJobModelTests
{
    [Fact]
    public void ImportJob_JsonDeserialization_ShouldWork()
    {
        // Arrange
        var json = """
            {
                "inputBlobUri": "https://example.com/input.ndjson",
                "outputBlobUri": "https://example.com/output.ndjson"
            }
            """;

        // Act
        var importJob = JsonSerializer.Deserialize<ImportJobRequest>(json);

        // Assert
        Assert.NotNull(importJob);
        Assert.Equal("https://example.com/input.ndjson", importJob.InputBlobUri.AbsoluteUri);
        Assert.Equal("https://example.com/output.ndjson", importJob.OutputBlobUri.AbsoluteUri);
    }

    [Fact]
    public void ImportJob_JsonSerialization_ShouldWork()
    {
        // Arrange
        var importJob = new ImportJob
        {
            Id = "test-job-123",
            InputBlobUri = new Uri("https://example.com/input.ndjson"),
            OutputBlobUri = new Uri("https://example.com/output.ndjson"),
        };

        // Act
        var json = JsonSerializer.Serialize(importJob);
        var deserializedJob = JsonSerializer.Deserialize<ImportJob>(json);

        // Assert
        Assert.NotNull(deserializedJob);
        Assert.Equal(importJob.Id, deserializedJob.Id);
        Assert.Equal(importJob.InputBlobUri.AbsoluteUri, deserializedJob.InputBlobUri.AbsoluteUri);
        Assert.Equal(
            importJob.OutputBlobUri.AbsoluteUri,
            deserializedJob.OutputBlobUri.AbsoluteUri
        );
    }
}
