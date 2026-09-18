using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Validation tests for scoped vector memory search options. These exercise the
/// contract that rejects malformed requests; they need no database because every
/// case fails validation before a connection is opened.
/// </summary>
public class MemorySearchValidationTests
{
    private static MemorySearchOptions Options(
        double[] vector,
        string embeddingProperty = "embedding",
        int limit = 10,
        string[]? modelIds = null,
        Dictionary<string, string>? filters = null,
        string? relatedTwinId = null,
        int? expectedDimension = null
    ) =>
        new()
        {
            Vector = vector,
            EmbeddingProperty = embeddingProperty,
            Limit = limit,
            ModelIds = modelIds,
            PropertyFilters = filters,
            RelatedTwinId = relatedTwinId,
            ExpectedDimension = expectedDimension,
        };

    [Fact]
    public void Validate_ValidOptions_Passes()
    {
        Options([0.1, 0.2, 0.3], modelIds: ["dtmi:test:One;1"], filters: new() { ["scopeKey"] = "a" })
            .Validate();
    }

    [Fact]
    public void Validate_EmptyVector_Throws()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => Options([]).Validate());
        Assert.Contains("at least one dimension", ex.Message);
    }

    [Fact]
    public void Validate_VectorAboveMaxDimensions_Throws()
    {
        double[] vector = new double[MemorySearchOptions.MaxVectorDimensions + 1];
        var ex = Assert.Throws<ValidationFailedException>(() => Options(vector).Validate());
        Assert.Contains("exceeds the maximum", ex.Message);
    }

    [Fact]
    public void Validate_NonFiniteVector_Throws()
    {
        var ex = Assert.Throws<ValidationFailedException>(
            () => Options([0.1, double.NaN]).Validate()
        );
        Assert.Contains("finite", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("properties.embedding")]
    [InlineData("embedding) OR 1=1 --")]
    [InlineData("1embedding")]
    public void Validate_InvalidEmbeddingProperty_Throws(string property)
    {
        Assert.Throws<ValidationFailedException>(
            () => Options([0.1], embeddingProperty: property).Validate()
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MemorySearchOptions.MaxLimit + 1)]
    public void Validate_OutOfRangeLimit_Throws(int limit)
    {
        var ex = Assert.Throws<ValidationFailedException>(
            () => Options([0.1], limit: limit).Validate()
        );
        Assert.Contains("Limit must be between", ex.Message);
    }

    [Fact]
    public void Validate_EmptyModelIdInAllowList_Throws()
    {
        Assert.Throws<ValidationFailedException>(
            () => Options([0.1], modelIds: ["dtmi:test:One;1", " "]).Validate()
        );
    }

    [Theory]
    [InlineData("scope key")]
    [InlineData("scope-key")]
    [InlineData("scope)")]
    [InlineData("1scope")]
    public void Validate_InvalidFilterKey_Throws(string key)
    {
        Assert.Throws<ValidationFailedException>(
            () => Options([0.1], filters: new() { [key] = "value" }).Validate()
        );
    }

    [Fact]
    public void Validate_BlankRelatedTwinId_Throws()
    {
        Assert.Throws<ValidationFailedException>(
            () => Options([0.1], relatedTwinId: " ").Validate()
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MemorySearchOptions.MaxVectorDimensions + 1)]
    public void Validate_ExpectedDimensionOutOfRange_Throws(int expected)
    {
        Assert.Throws<ValidationFailedException>(
            () => Options([0.1, 0.2], expectedDimension: expected).Validate()
        );
    }

    [Fact]
    public void Validate_ExpectedDimensionMismatch_Throws()
    {
        var ex = Assert.Throws<ValidationFailedException>(
            () => Options([0.1, 0.2, 0.3], expectedDimension: 4).Validate()
        );
        Assert.Contains("3 dimensions", ex.Message);
    }

    [Fact]
    public void ValidateIndex_ValidOptions_Passes()
    {
        new MemorySearchIndexOptions { EmbeddingProperty = "embedding", Dimension = 1536 }.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(MemorySearchOptions.MaxVectorDimensions + 1)]
    public void ValidateIndex_InvalidDimension_Throws(int dimension)
    {
        Assert.Throws<ValidationFailedException>(
            () => new MemorySearchIndexOptions { Dimension = dimension }.Validate()
        );
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(101, null)]
    [InlineData(null, 3)]
    [InlineData(null, 1001)]
    public void ValidateIndex_InvalidHnswParameters_Throws(int? m, int? efConstruction)
    {
        Assert.Throws<ValidationFailedException>(
            () =>
                new MemorySearchIndexOptions
                {
                    Dimension = 3,
                    M = m,
                    EfConstruction = efConstruction,
                }.Validate()
        );
    }

    [Fact]
    public void ValidateIndex_InvalidEmbeddingProperty_Throws()
    {
        Assert.Throws<ValidationFailedException>(
            () => new MemorySearchIndexOptions { EmbeddingProperty = "bad-name", Dimension = 3 }.Validate()
        );
    }
}
