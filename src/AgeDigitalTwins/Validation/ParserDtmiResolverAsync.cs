using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using AgeDigitalTwins.Models;
using DTDLParser;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins.Validation;

internal static class NpgsqlDataSourceExtensions
{
    public static async IAsyncEnumerable<string> ParserDtmiResolverAsync(
        this NpgsqlDataSource dataSource,
        string graphName,
        MemoryCache modelCache,
        TimeSpan modelCacheExpiration,
        IReadOnlyCollection<Dtmi> dtmis,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        List<Dtmi> dtmisToFetch = new List<Dtmi>();
        // Check the cache for existing models
        foreach (var dtmi in dtmis)
        {
            if (
                modelCache.TryGetValue(dtmi.AbsoluteUri, out DigitalTwinsModelData? modelData)
                && modelData?.DtdlModel is not null
            )
            {
                // If the model is already in the cache, yield it directly
                yield return modelData.DtdlModel!;
            }
            else
            {
                dtmisToFetch.Add(dtmi);
            }
        }

        string dtmiList = string.Join(",", dtmisToFetch.Select(d => $"'{d}'"));
        string cypher = $"MATCH (m:Model) WHERE m.id IN [{dtmiList}] RETURN m";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype>(0).ConfigureAwait(false);
            var vertex = agResult.GetVertex();
            var modelData = new DigitalTwinsModelData(vertex.Properties);
            if (modelCacheExpiration != TimeSpan.Zero && modelData.DtdlModel is not null)
            {
                // Store the model in the cache with the specified expiration
                modelCache.Set(
                    modelData.Id,
                    modelData,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = modelCacheExpiration,
                    }
                );
            }
            yield return modelData.DtdlModel!;
        }
    }
}
