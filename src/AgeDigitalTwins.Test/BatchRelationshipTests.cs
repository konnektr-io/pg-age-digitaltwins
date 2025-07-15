using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgeDigitalTwins.Models;
using Xunit;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

public class BatchRelationshipTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public BatchRelationshipTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipsAsync_WithValidRelationships_ShouldSucceed()
    {
        // Arrange
        var modelId = "dtmi:com:example:TestModel;1";
        var model = $$"""
            {
                "@id": "{{modelId}}",
                "@type": "Interface",
                "@context": "dtmi:dtdl:context;2",
                "contents": [
                    {
                        "@type": "Property",
                        "name": "temperature",
                        "schema": "double"
                    },
                    {
                        "@type": "Relationship",
                        "name": "contains",
                        "target": "{{modelId}}"
                    }
                ]
            }
            """;

        // Create the model first
        await Client.CreateModelsAsync([model]);

        // Create twins first
        var sourceTwin = JsonSerializer.Serialize(new { 
            __dtId = "sourceTwin", 
            __metadata = new { __model = modelId }, 
            temperature = 25.5 
        }).Replace("__", "$");
        
        var targetTwin = JsonSerializer.Serialize(new { 
            __dtId = "targetTwin", 
            __metadata = new { __model = modelId }, 
            temperature = 30.0 
        }).Replace("__", "$");

        await Client.CreateOrReplaceDigitalTwinAsync("sourceTwin", sourceTwin);
        await Client.CreateOrReplaceDigitalTwinAsync("targetTwin", targetTwin);

        // Create relationships
        var relationships = new List<string>
        {
            JsonSerializer.Serialize(new { 
                __sourceId = "sourceTwin", 
                __targetId = "targetTwin", 
                __relationshipId = "rel1", 
                __relationshipName = "contains" 
            }).Replace("__", "$"),
            JsonSerializer.Serialize(new { 
                __sourceId = "targetTwin", 
                __targetId = "sourceTwin", 
                __relationshipId = "rel2", 
                __relationshipName = "contains" 
            }).Replace("__", "$")
        };

        // Act
        var result = await Client.CreateOrReplaceRelationshipsAsync(relationships);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.False(result.HasFailures);
        Assert.Equal(2, result.Results.Count);

        foreach (var operationResult in result.Results)
        {
            Assert.True(operationResult.IsSuccess);
            Assert.Null(operationResult.ErrorMessage);
            Assert.Contains(operationResult.RelationshipId, new[] { "rel1", "rel2" });
        }

        _output.WriteLine(
            $"Batch relationship operation completed successfully: {result.SuccessCount} successes, {result.FailureCount} failures"
        );
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipsAsync_WithInvalidRelationships_ShouldReportFailures()
    {
        // Arrange
        var modelId = "dtmi:com:example:TestModel;1";
        var model = $$"""
            {
                "@id": "{{modelId}}",
                "@type": "Interface",
                "@context": "dtmi:dtdl:context;2",
                "contents": [
                    {
                        "@type": "Property",
                        "name": "temperature",
                        "schema": "double"
                    },
                    {
                        "@type": "Relationship",
                        "name": "contains",
                        "target": "{{modelId}}"
                    }
                ]
            }
            """;

        // Create the model first
        await Client.CreateModelsAsync([model]);

        // Create only one twin
        var sourceTwin = JsonSerializer.Serialize(new { 
            __dtId = "sourceTwin", 
            __metadata = new { __model = modelId }, 
            temperature = 25.5 
        }).Replace("__", "$");

        await Client.CreateOrReplaceDigitalTwinAsync("sourceTwin", sourceTwin);

        // Create relationships - one valid, one with non-existent target
        var relationships = new List<string>
        {
            JsonSerializer.Serialize(new { 
                __sourceId = "sourceTwin", 
                __targetId = "nonExistentTwin", 
                __relationshipId = "rel1", 
                __relationshipName = "contains" 
            }).Replace("__", "$"),
            JsonSerializer.Serialize(new { 
                __sourceId = "nonExistentSource", 
                __targetId = "sourceTwin", 
                __relationshipId = "rel2", 
                __relationshipName = "contains" 
            }).Replace("__", "$")
        };

        // Act
        var result = await Client.CreateOrReplaceRelationshipsAsync(relationships);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.True(result.HasFailures);

        var failedResults = result.Results.Where(r => !r.IsSuccess).ToList();
        Assert.Equal(2, failedResults.Count);

        foreach (var failedResult in failedResults)
        {
            Assert.False(failedResult.IsSuccess);
            Assert.NotNull(failedResult.ErrorMessage);
            Assert.Contains("does not exist", failedResult.ErrorMessage);
        }

        _output.WriteLine(
            $"Batch relationship operation completed with expected failures: {result.SuccessCount} successes, {result.FailureCount} failures"
        );
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipsAsync_WithEmptyBatch_ShouldReturnEmptyResult()
    {
        // Arrange
        var relationships = new List<string>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<System.ArgumentException>(
            () => Client.CreateOrReplaceRelationshipsAsync(relationships)
        );

        Assert.Contains("Relationships cannot be empty", exception.Message);
        _output.WriteLine("Empty batch operation threw expected exception");
    }

    [Fact]
    public async Task CreateOrReplaceRelationshipsAsync_WithOversizedBatch_ShouldThrowException()
    {
        // Arrange
        var relationships = new List<string>();
        for (int i = 0; i < 101; i++) // Exceed the limit of 100
        {
            relationships.Add(
                JsonSerializer.Serialize(new { 
                    __sourceId = $"source{i}", 
                    __targetId = $"target{i}", 
                    __relationshipId = $"rel{i}", 
                    __relationshipName = "contains" 
                }).Replace("__", "$")
            );
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<System.ArgumentException>(
            () => Client.CreateOrReplaceRelationshipsAsync(relationships)
        );

        Assert.Contains("Cannot process more than 100 relationships", exception.Message);
        _output.WriteLine($"Expected exception thrown: {exception.Message}");
    }
}
