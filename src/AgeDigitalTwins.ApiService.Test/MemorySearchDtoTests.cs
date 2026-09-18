using System;
using System.Text.Json;
using AgeDigitalTwins.ApiService.Models;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test;

/// <summary>
/// Contract tests for the scoped vector memory search DTOs. The API serializes with
/// camelCase naming and omits null values (see Program.cs ConfigureHttpJsonOptions);
/// these tests lock that documented wire shape.
/// </summary>
public class MemorySearchDtoTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void MemorySearchRequest_Deserializes_DocumentedPayload()
    {
        var json = """
            {
                "vector": [0.11, -0.02, 0.58],
                "embeddingProperty": "embedding",
                "limit": 5,
                "modelIds": ["dtmi:example:MemoryRecord;1"],
                "propertyFilters": { "scopeKey": "workspace-a", "status": "active" },
                "relatedTwinId": "session-9",
                "expectedDimension": 3,
                "excerptLength": 500
            }
            """;

        var request = JsonSerializer.Deserialize<MemorySearchRequest>(json, WebOptions);

        Assert.NotNull(request);
        Assert.NotNull(request!.Vector);
        Assert.Equal(3, request.Vector!.Length);
        Assert.Equal(0.11, request.Vector[0], 6);
        Assert.Equal("embedding", request.EmbeddingProperty);
        Assert.Equal(5, request.Limit);
        Assert.NotNull(request.ModelIds);
        Assert.Equal(["dtmi:example:MemoryRecord;1"], request.ModelIds!);
        Assert.NotNull(request.PropertyFilters);
        Assert.Equal("workspace-a", request.PropertyFilters!["scopeKey"]);
        Assert.Equal("active", request.PropertyFilters["status"]);
        Assert.Equal("session-9", request.RelatedTwinId);
        Assert.Equal(3, request.ExpectedDimension);
        Assert.Equal(500, request.ExcerptLength);
    }

    [Fact]
    public void MemorySearchRequest_MinimalPayload_LeavesOptionalFieldsUnset()
    {
        var request = JsonSerializer.Deserialize<MemorySearchRequest>(
            """{ "vector": [0.5, 0.5] }""",
            WebOptions
        );

        Assert.NotNull(request);
        Assert.Null(request!.EmbeddingProperty);
        Assert.Null(request.Limit);
        Assert.Null(request.ModelIds);
        Assert.Null(request.PropertyFilters);
        Assert.Null(request.RelatedTwinId);
    }

    [Fact]
    public void MemorySearchResultDto_Serializes_CamelCaseFields()
    {
        var dto = new MemorySearchResultDto(
            "memory-42",
            "dtmi:example:MemoryRecord;1",
            0.013,
            """{"scopeKey":"workspace-a"}""",
            DateTimeOffset.Parse("2026-09-18T08:12:44+00:00")
        );

        var json = JsonSerializer.Serialize(dto, WebOptions);

        Assert.Contains("\"id\":\"memory-42\"", json);
        Assert.Contains("\"modelId\":\"dtmi:example:MemoryRecord;1\"", json);
        Assert.Contains("\"distance\":0.013", json);
        Assert.Contains("\"excerpt\":", json);
        Assert.Contains("\"lastUpdatedOn\":", json);
    }

    [Fact]
    public void MemorySearchResultDto_OmitsNullModelAndTimestamp()
    {
        var json = JsonSerializer.Serialize(
            new MemorySearchResultDto("memory-42", null, 0.5, "{}", null),
            WebOptions
        );

        Assert.DoesNotContain("modelId", json);
        Assert.DoesNotContain("lastUpdatedOn", json);
    }

    [Fact]
    public void MemorySearchIndexRequest_Deserializes_DocumentedPayload()
    {
        var request = JsonSerializer.Deserialize<MemorySearchIndexRequest>(
            """{ "embeddingProperty": "embedding", "dimension": 1536, "m": 16, "efConstruction": 64 }""",
            WebOptions
        );

        Assert.NotNull(request);
        Assert.Equal("embedding", request!.EmbeddingProperty);
        Assert.Equal(1536, request.Dimension);
        Assert.Equal(16, request.M);
        Assert.Equal(64, request.EfConstruction);
    }

    [Fact]
    public void MemorySearchIndex_Serializes_CamelCaseFields()
    {
        var json = JsonSerializer.Serialize(new MemorySearchIndex("twin_embedding_hnsw_idx", 1536), WebOptions);

        Assert.Contains("\"indexName\":\"twin_embedding_hnsw_idx\"", json);
        Assert.Contains("\"dimension\":1536", json);
    }

    [Fact]
    public void MemorySearchCapability_Serializes_CamelCaseField()
    {
        var json = JsonSerializer.Serialize(new MemorySearchCapability(true), WebOptions);

        Assert.Contains("\"vectorSearchAvailable\":true", json);
    }
}
