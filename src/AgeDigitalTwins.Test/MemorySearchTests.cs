using System.Text.Json.Nodes;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using Npgsql;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Integration tests for scoped vector memory search. The essential contract is
/// that every scope predicate is applied in the database <b>before</b>
/// nearest-neighbour ranking and LIMIT, so out-of-scope records can never appear.
/// These tests require a pgvector-enabled image (see CnpgOnlyFact).
/// </summary>
public class MemorySearchTests : TestBase
{
    private const string RecordModelId = "dtmi:test:memoryrecord;1";
    private const string OtherRecordModelId = "dtmi:test:otherrecord;1";
    private const string GroupModelId = "dtmi:test:memorygroup;1";

    private const string RecordModel = """
        {
          "@context": "dtmi:dtdl:context;3",
          "@id": "dtmi:test:memoryrecord;1",
          "@type": "Interface",
          "displayName": "MemoryRecord",
          "contents": [
            { "@type": "Property", "name": "embedding", "schema": { "@type": "Array", "elementSchema": "double" } },
            { "@type": "Property", "name": "scopeKey", "schema": "string" },
            { "@type": "Property", "name": "ownerId", "schema": "string" },
            { "@type": "Property", "name": "status", "schema": "string" },
            { "@type": "Property", "name": "content", "schema": "string" },
            { "@type": "Relationship", "name": "rel_partof", "target": "dtmi:test:memorygroup;1" }
          ]
        }
        """;

    private const string OtherRecordModel = """
        {
          "@context": "dtmi:dtdl:context;3",
          "@id": "dtmi:test:otherrecord;1",
          "@type": "Interface",
          "displayName": "OtherRecord",
          "contents": [
            { "@type": "Property", "name": "embedding", "schema": { "@type": "Array", "elementSchema": "double" } },
            { "@type": "Property", "name": "scopeKey", "schema": "string" },
            { "@type": "Property", "name": "status", "schema": "string" }
          ]
        }
        """;

    private const string GroupModel = """
        {
          "@context": "dtmi:dtdl:context;3",
          "@id": "dtmi:test:memorygroup;1",
          "@type": "Interface",
          "displayName": "MemoryGroup",
          "contents": [
            { "@type": "Property", "name": "name", "schema": "string" }
          ]
        }
        """;

    private static string RecordJson(
        string id,
        double[]? embedding,
        string modelId = RecordModelId,
        string scopeKey = "scope-a",
        string? ownerId = "owner-a",
        string status = "active",
        string content = "content"
    )
    {
        var node = new JsonObject
        {
            ["$dtId"] = id,
            ["$metadata"] = new JsonObject { ["$model"] = modelId },
            ["scopeKey"] = scopeKey,
            ["status"] = status,
        };

        // ownerId/content are only declared by the primary record model.
        if (modelId == RecordModelId && ownerId != null)
        {
            node["ownerId"] = ownerId;
        }

        if (modelId == RecordModelId)
        {
            node["content"] = content;
        }

        if (embedding != null)
        {
            var array = new JsonArray();
            foreach (double value in embedding)
            {
                array.Add(value);
            }

            node["embedding"] = array;
        }

        return node.ToJsonString();
    }

    private async Task DropIndexAsync(string indexName)
    {
        await using var connection = await Client.GetDataSource().OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            $"DROP INDEX IF EXISTS \"{Client.GetGraphName()}\".\"{indexName}\"",
            connection
        );
        await command.ExecuteNonQueryAsync();
    }

    private async Task LoadModelsAsync()
    {
        await Client.CreateModelsAsync([RecordModel, OtherRecordModel, GroupModel]);
    }

    [CnpgOnlyFact]
    public async Task MemorySearch_RanksByDistance_ClosestFirst()
    {
        await LoadModelsAsync();
        await Client.CreateOrReplaceDigitalTwinAsync("r1", RecordJson("r1", [1.0, 0.0, 0.0]));
        await Client.CreateOrReplaceDigitalTwinAsync("r2", RecordJson("r2", [0.0, 1.0, 0.0]));
        await Client.CreateOrReplaceDigitalTwinAsync("r3", RecordJson("r3", [0.0, 0.0, 1.0]));

        var results = await Client.MemorySearchAsync(
            new MemorySearchOptions { Vector = [0.9, 0.1, 0.0], Limit = 3 }
        );

        Assert.Equal(3, results.Count);
        Assert.Equal("r1", results[0].Twin.Id);
        Assert.Equal("r2", results[1].Twin.Id);
        Assert.True(results[0].Distance <= results[1].Distance);

        var r3 = results.Single(r => r.Twin.Id == "r3");
        Assert.False(string.IsNullOrEmpty(r3.Twin.Metadata.ModelId));
    }

    [CnpgOnlyFact]
    public async Task MemorySearch_ScopePredicateAppliedBeforeRankingAndLimit()
    {
        await LoadModelsAsync();

        // The out-of-scope record is the CLOSEST match, so a filter applied after
        // ranking/LIMIT would leak it as the single top result.
        await Client.CreateOrReplaceDigitalTwinAsync(
            "in-scope",
            RecordJson("in-scope", [0.9, 0.0, 0.0], scopeKey: "scope-a")
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "out-of-scope",
            RecordJson("out-of-scope", [1.0, 0.0, 0.0], scopeKey: "scope-b")
        );

        var results = await Client.MemorySearchAsync(
            new MemorySearchOptions
            {
                Vector = [1.0, 0.0, 0.0],
                Limit = 1,
                PropertyFilters = new Dictionary<string, string> { ["scopeKey"] = "scope-a" },
            }
        );

        Assert.Single(results);
        Assert.Equal("in-scope", results[0].Twin.Id);

        var unbounded = await Client.MemorySearchAsync(
            new MemorySearchOptions
            {
                Vector = [1.0, 0.0, 0.0],
                Limit = 10,
                PropertyFilters = new Dictionary<string, string> { ["scopeKey"] = "scope-a" },
            }
        );

        Assert.DoesNotContain(unbounded, r => r.Twin.Id == "out-of-scope");

        var otherScope = await Client.MemorySearchAsync(
            new MemorySearchOptions
            {
                Vector = [1.0, 0.0, 0.0],
                Limit = 1,
                PropertyFilters = new Dictionary<string, string> { ["scopeKey"] = "scope-b" },
            }
        );

        Assert.Single(otherScope);
        Assert.Equal("out-of-scope", otherScope[0].Twin.Id);
    }

    [CnpgOnlyFact]
    public async Task MemorySearch_OwnerPredicateAndStatusFilter_AreApplied()
    {
        await LoadModelsAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "owner-a",
            RecordJson("owner-a", [1.0, 0.0, 0.0], ownerId: "owner-a", status: "active")
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "owner-b",
            RecordJson("owner-b", [1.0, 0.0, 0.0], ownerId: "owner-b", status: "active")
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "archived",
            RecordJson("archived", [1.0, 0.0, 0.0], ownerId: "owner-a", status: "archived")
        );

        var results = await Client.MemorySearchAsync(
            new MemorySearchOptions
            {
                Vector = [1.0, 0.0, 0.0],
                Limit = 10,
                PropertyFilters = new Dictionary<string, string>
                {
                    ["ownerId"] = "owner-a",
                    ["status"] = "active",
                },
            }
        );

        Assert.Single(results);
        Assert.Equal("owner-a", results[0].Twin.Id);
    }

    [CnpgOnlyFact]
    public async Task MemorySearch_ModelAllowList_ExcludesOtherModels()
    {
        await LoadModelsAsync();
        await Client.CreateOrReplaceDigitalTwinAsync("record", RecordJson("record", [1.0, 0.0, 0.0]));
        await Client.CreateOrReplaceDigitalTwinAsync(
            "other",
            RecordJson("other", [1.0, 0.0, 0.0], modelId: OtherRecordModelId)
        );

        var results = await Client.MemorySearchAsync(
            new MemorySearchOptions { Vector = [1.0, 0.0, 0.0], Limit = 10, ModelIds = [RecordModelId] }
        );

        Assert.Single(results);
        Assert.Equal("record", results[0].Twin.Id);
        Assert.Equal(RecordModelId, results[0].Twin.Metadata.ModelId);
    }

    [CnpgOnlyFact]
    public async Task MemorySearch_RelatedTwinPredicate_ReturnsOnlyRelatedTwins()
    {
        await LoadModelsAsync();
        await Client.CreateOrReplaceDigitalTwinAsync(
            "group1",
            new JsonObject
            {
                ["$dtId"] = "group1",
                ["$metadata"] = new JsonObject { ["$model"] = GroupModelId },
                ["name"] = "group one",
            }.ToJsonString()
        );
        await Client.CreateOrReplaceDigitalTwinAsync("related1", RecordJson("related1", [1.0, 0.0, 0.0]));
        await Client.CreateOrReplaceDigitalTwinAsync("related2", RecordJson("related2", [0.0, 1.0, 0.0]));
        await Client.CreateOrReplaceDigitalTwinAsync("unrelated", RecordJson("unrelated", [1.0, 0.0, 0.0]));

        await Client.CreateOrReplaceRelationshipAsync(
            "related1",
            "rel-1",
            """{ "$relationshipName": "rel_partof", "$targetId": "group1" }"""
        );
        await Client.CreateOrReplaceRelationshipAsync(
            "related2",
            "rel-2",
            """{ "$relationshipName": "rel_partof", "$targetId": "group1" }"""
        );

        var results = await Client.MemorySearchAsync(
            new MemorySearchOptions
            {
                Vector = [1.0, 0.0, 0.0],
                Limit = 10,
                RelatedTwinId = "group1",
            }
        );

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Twin.Id == "related1");
        Assert.Contains(results, r => r.Twin.Id == "related2");
        Assert.DoesNotContain(results, r => r.Twin.Id == "unrelated");
        Assert.DoesNotContain(results, r => r.Twin.Id == "group1");
    }

    [CnpgOnlyFact]
    public async Task MemorySearch_TwinsWithoutEmbedding_DoNotConsumeLimit()
    {
        await LoadModelsAsync();
        await Client.CreateOrReplaceDigitalTwinAsync("with1", RecordJson("with1", [1.0, 0.0, 0.0]));
        await Client.CreateOrReplaceDigitalTwinAsync("with2", RecordJson("with2", [0.9, 0.1, 0.0]));
        await Client.CreateOrReplaceDigitalTwinAsync("without", RecordJson("without", null));

        var results = await Client.MemorySearchAsync(
            new MemorySearchOptions { Vector = [1.0, 0.0, 0.0], Limit = 2 }
        );

        Assert.Equal(2, results.Count);
        Assert.DoesNotContain(results, r => r.Twin.Id == "without");
    }

    [CnpgOnlyFact]
    public async Task MemorySearch_NoMatch_ReturnsEmpty()
    {
        await LoadModelsAsync();
        await Client.CreateOrReplaceDigitalTwinAsync("r1", RecordJson("r1", [1.0, 0.0, 0.0]));

        var results = await Client.MemorySearchAsync(
            new MemorySearchOptions
            {
                Vector = [1.0, 0.0, 0.0],
                Limit = 5,
                PropertyFilters = new Dictionary<string, string> { ["scopeKey"] = "no-such-scope" },
            }
        );

        Assert.Empty(results);
    }

    [CnpgOnlyFact]
    public async Task EnsureMemorySearchIndex_IsIdempotent_AndSearchWorksWithIndex()
    {
        await LoadModelsAsync();
        await Client.CreateOrReplaceDigitalTwinAsync("r1", RecordJson("r1", [0.5, 0.5, 0.5]));

        string first = await Client.EnsureMemorySearchIndexAsync(
            new MemorySearchIndexOptions { EmbeddingProperty = "embedding", Dimension = 3 }
        );
        string second = await Client.EnsureMemorySearchIndexAsync(
            new MemorySearchIndexOptions { EmbeddingProperty = "embedding", Dimension = 3 }
        );

        Assert.Equal(first, second);

        var results = await Client.MemorySearchAsync(
            new MemorySearchOptions { Vector = [0.5, 0.5, 0.5], Limit = 1, ExpectedDimension = 3 }
        );

        Assert.Single(results);
        Assert.Equal("r1", results[0].Twin.Id);

        var other = await Client.EnsureMemorySearchIndexAsync(
            new MemorySearchIndexOptions
            {
                EmbeddingProperty = "embedding",
                Dimension = 3,
                M = 16,
                EfConstruction = 64,
            }
        );

        Assert.Equal(first, other);

        await DropIndexAsync(first);
    }

    [CnpgOnlyFact]
    public async Task MemorySearch_DimensionMismatchAgainstIndex_Throws()
    {
        await LoadModelsAsync();
        await Client.CreateOrReplaceDigitalTwinAsync("r1", RecordJson("r1", [0.5, 0.5, 0.5]));

        string indexName = await Client.EnsureMemorySearchIndexAsync(
            new MemorySearchIndexOptions { EmbeddingProperty = "embedding", Dimension = 3 }
        );

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(
            () =>
                Client.MemorySearchAsync(
                    new MemorySearchOptions { Vector = [0.5, 0.5, 0.5, 0.5], Limit = 1 }
                )
        );

        Assert.Contains("dimensions", ex.Message);
        await DropIndexAsync(indexName);
    }

    [Fact]
    public async Task MemorySearch_WithoutPgVector_ReportsCapabilityError()
    {
        if (await Client.IsVectorSearchAvailableAsync())
        {
            // pgvector present (pgvector-enabled image): the contract below is
            // covered by the integration tests instead.
            Assert.True(true);
            return;
        }

        await Assert.ThrowsAsync<PgVectorNotAvailableException>(
            () => Client.MemorySearchAsync(new MemorySearchOptions { Vector = [0.1, 0.2] })
        );

        await Assert.ThrowsAsync<PgVectorNotAvailableException>(
            () =>
                Client.EnsureMemorySearchIndexAsync(
                    new MemorySearchIndexOptions { EmbeddingProperty = "embedding", Dimension = 2 }
                )
        );
    }
}
