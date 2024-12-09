using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using AgeDigitalTwins.Models;
using DTDLParser;
using Npgsql;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins.Validation;

internal static class ModelsRepositoryClientExtensions
{
    public static async IAsyncEnumerable<string> ParserDtmiResolverAsync(
        this NpgsqlDataSource dataSource,
        string graphName,
        IReadOnlyCollection<Dtmi> dtmis,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        string dtmiList = string.Join(",", dtmis.Select(d => $"'{d}'"));
        string cypher = $"MATCH (m:Model) WHERE m.id IN [{dtmiList}] RETURN m";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCypherCommand(graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype>(0).ConfigureAwait(false);
            var vertex = agResult.GetVertex();
            var modelData = new DigitalTwinsModelData(vertex.Properties);
            yield return modelData.DtdlModel;
        }
    }
}
