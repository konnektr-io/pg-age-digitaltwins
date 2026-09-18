using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using Npgsql;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    private static readonly Regex IndexedVectorDimensionPattern =
        new(@"vector\((\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Checks whether the pgvector extension is installed in the backing database.
    /// Scoped vector memory search requires it.
    /// </summary>
    public virtual async Task<bool> IsVectorSearchAvailableAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'",
            connection
        );
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is long count ? count > 0 : Convert.ToInt64(scalar) > 0;
    }

    /// <summary>
    /// Creates the HNSW index backing scoped vector memory search for one twin
    /// embedding property. Idempotent (<c>CREATE INDEX IF NOT EXISTS</c>).
    /// </summary>
    /// <param name="options">Index options (embedding property, dimension, HNSW tuning).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name of the (possibly pre-existing) index.</returns>
    /// <exception cref="ValidationFailedException">Invalid options.</exception>
    /// <exception cref="PgVectorNotAvailableException">pgvector is not installed.</exception>
    public virtual async Task<string> EnsureMemorySearchIndexAsync(
        MemorySearchIndexOptions options,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "EnsureMemorySearchIndexAsync",
            ActivityKind.Client
        );

        try
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();
            await EnsureVectorSearchAvailableAsync(cancellationToken).ConfigureAwait(false);

            string indexName = GetMemorySearchIndexName(options.EmbeddingProperty);
            string withClause = BuildHnswWithClause(options);

            // Mirrors the proven HNSW expression used for model-embedding search:
            // the twin vertex property bag is projected to text and cast to vector(n).
            string sql =
                $@"CREATE INDEX IF NOT EXISTS ""{indexName}"" ON ""{_graphName}"".""Twin"" "
                + $@"USING hnsw ((ag_catalog.agtype_access_operator(properties, '""{options.EmbeddingProperty}""'::agtype)::text::vector({options.Dimension})) vector_l2_ops)"
                + withClause;

            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return indexName;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Performs a scoped vector search over digital twins. Scope predicates
    /// (model allow-list, property equality filters, related-twin predicate) are
    /// applied in the database before nearest-neighbour ranking and
    /// <c>LIMIT</c>. Existing generic hybrid search behavior is unchanged.
    /// </summary>
    /// <param name="options">Search options (query vector, scope predicates, limit).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Ranked records (closest first) with L2 distance scores.</returns>
    /// <exception cref="ValidationFailedException">Invalid options or dimension mismatch.</exception>
    /// <exception cref="PgVectorNotAvailableException">pgvector is not installed.</exception>
    public virtual async Task<IReadOnlyList<MemorySearchResult>> MemorySearchAsync(
        MemorySearchOptions options,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("MemorySearchAsync", ActivityKind.Client);

        try
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();
            await EnsureVectorSearchAvailableAsync(cancellationToken).ConfigureAwait(false);
            await ValidateVectorAgainstIndexAsync(options, cancellationToken).ConfigureAwait(false);

            var (cypher, parameters) = BuildMemorySearchCypher(options);

            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.PreferStandby,
                cancellationToken
            );
            await using var command = connection.CreateCypherCommand(_graphName, cypher, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var results = new List<MemorySearchResult>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var vertex = (Vertex)(await reader.GetFieldValueAsync<Agtype?>(0))!;
                var distanceAgtype = await reader.GetFieldValueAsync<Agtype?>(1);
                var (distanceValue, _) = ConvertAgtypeToObject((Agtype)distanceAgtype!);
                double distance = Convert.ToDouble(distanceValue, CultureInfo.InvariantCulture);

                string twinJson = JsonSerializer.Serialize(vertex!.Properties);
                var twin =
                    JsonSerializer.Deserialize<BasicDigitalTwin>(twinJson)
                    ?? throw new InvalidOperationException("Memory search returned an unreadable twin.");

                results.Add(new MemorySearchResult(twin, distance));
            }

            return results;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task EnsureVectorSearchAvailableAsync(CancellationToken cancellationToken)
    {
        if (!await IsVectorSearchAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new PgVectorNotAvailableException(
                "pgvector is not installed in the backing database. "
                    + "Scoped vector memory search requires the 'vector' extension: use a "
                    + "pgvector-enabled database image or run 'CREATE EXTENSION vector;' "
                    + "before calling this API."
            );
        }
    }

    private async Task ValidateVectorAgainstIndexAsync(
        MemorySearchOptions options,
        CancellationToken cancellationToken
    )
    {
        string indexName = GetMemorySearchIndexName(options.EmbeddingProperty);

        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );
        await using var command = new NpgsqlCommand(
            "SELECT indexdef FROM pg_indexes WHERE indexname = $1",
            connection
        );
        command.Parameters.AddWithValue(indexName);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);

        if (scalar is string indexDef)
        {
            Match match = IndexedVectorDimensionPattern.Match(indexDef);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int indexedDimension))
            {
                if (indexedDimension != options.Vector.Length)
                {
                    throw new ValidationFailedException(
                        $"Query vector has {options.Vector.Length} dimensions but the index "
                            + $"'{indexName}' on property '{options.EmbeddingProperty}' was built "
                            + $"for {indexedDimension} dimensions."
                    );
                }
            }
        }
    }

    private static string GetMemorySearchIndexName(string embeddingProperty)
    {
        string slug = embeddingProperty.ToLowerInvariant();
        if (slug.Length > 40)
        {
            slug = slug[..40];
        }

        return $"twin_{slug}_hnsw_idx";
    }

    private static string BuildHnswWithClause(MemorySearchIndexOptions options)
    {
        var parts = new List<string>();
        if (options.M.HasValue)
        {
            parts.Add($"m = {options.M.Value}");
        }

        if (options.EfConstruction.HasValue)
        {
            parts.Add($"ef_construction = {options.EfConstruction.Value}");
        }

        return parts.Count == 0 ? string.Empty : $" WITH ({string.Join(", ", parts)})";
    }

    private static (string Cypher, Dictionary<string, object?> Parameters) BuildMemorySearchCypher(
        MemorySearchOptions options
    )
    {
        var parameters = new Dictionary<string, object?>();
        var conditions = new List<string>
        {
            // Twins without a stored embedding must never consume LIMIT slots.
            $"t.`{options.EmbeddingProperty}` IS NOT NULL",
        };

        if (options.ModelIds is { Count: > 0 } modelIds)
        {
            var modelClauses = new List<string>();
            for (int i = 0; i < modelIds.Count; i++)
            {
                string paramName = $"msmodel_{i}";
                modelClauses.Add($"t.`$metadata`.`$model` = ${paramName}");
                parameters[paramName] = modelIds[i];
            }

            conditions.Add($"({string.Join(" OR ", modelClauses)})");
        }

        if (options.PropertyFilters is { Count: > 0 } filters)
        {
            int index = 0;
            foreach (var filter in filters)
            {
                string paramName = $"msprop_{index++}";
                conditions.Add($"t.`{filter.Key}` = ${paramName}");
                parameters[paramName] = filter.Value;
            }
        }

        string matchClause = options.RelatedTwinId != null
            ? "MATCH (t:Twin)-[]-(related:Twin {`$dtId`: $relatedTwinId})"
            : "MATCH (t:Twin)";

        if (options.RelatedTwinId != null)
        {
            parameters["relatedTwinId"] = options.RelatedTwinId;
        }

        string whereClause = $" WHERE {string.Join(" AND ", conditions)}";

        // The serialized vector contains only JSON numbers, so inline interpolation
        // is injection-safe and mirrors the existing twin hybrid search.
        string vectorString = JsonSerializer.Serialize(options.Vector);
        string distanceExpression = $"l2_distance(t.`{options.EmbeddingProperty}`, {vectorString}::vector)";

        string cypher =
            $"{matchClause}"
            + $"{whereClause} "
            + $"RETURN t, {distanceExpression} AS distance "
            + $"ORDER BY {distanceExpression} ASC "
            + $"LIMIT {options.Limit}";

        return (cypher, parameters);
    }
}
