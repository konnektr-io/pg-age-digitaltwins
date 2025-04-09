using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using DTDLParser;
using DTDLParser.Models;
using Npgsql;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    public static async IAsyncEnumerable<string> ConvertToAsyncEnumerable(
        IEnumerable<string> source
    )
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }

    public virtual async IAsyncEnumerable<DigitalTwinsModelData> GetModelsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // TODO: Implement dependenciesFor parameter
        string cypher = $@"MATCH (m:Model) RETURN m";
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
            var vertex = (Vertex)agResult;
            yield return new DigitalTwinsModelData(vertex.Properties);
        }
    }

    public virtual async Task<DigitalTwinsModelData> GetModelAsync(
        string modelId,
        CancellationToken cancellationToken = default
    )
    {
        string cypher = $@"MATCH (m:Model) WHERE m.id = '{modelId}' RETURN m";
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
            var vertex = (Vertex)agResult;
            return new DigitalTwinsModelData(vertex.Properties);
        }
        else
        {
            throw new ModelNotFoundException($"Model with ID {modelId} not found");
        }
    }

    public virtual async Task<IReadOnlyList<DigitalTwinsModelData>> CreateModelsAsync(
        IEnumerable<string> dtdlModels,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var parsedModels = await _modelParser.ParseAsync(
                ConvertToAsyncEnumerable(dtdlModels),
                cancellationToken: cancellationToken
            );
            IEnumerable<DigitalTwinsModelData> modelDatas = dtdlModels.Select(
                dtdlModel => new DigitalTwinsModelData(dtdlModel)
            );
            // This is needed as after unwinding, it gets converted to agtype again
            string modelsString =
                $"['{string.Join("','", modelDatas.Select(m => JsonSerializer.Serialize(m, serializerOptions).Replace("'", "\\'")))}']";

            // It is not possible to update or overwrite an existing model
            // Trying so will raise a unique constraint violation
            string cypher =
                $@"UNWIND {modelsString} as model
            WITH model::agtype as modelAgtype
            CREATE (m:Model {{id: modelAgtype['id']}})
            SET m = modelAgtype
            RETURN m";

            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await using var command = connection.CreateCypherCommand(_graphName, cypher);

            List<DigitalTwinsModelData> result = [];
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                int k = 0;
                while (await reader.ReadAsync(cancellationToken))
                {
                    var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                    var vertex = (Vertex)agResult;
                    result.Add(new DigitalTwinsModelData(vertex.Properties));
                    k++;
                }

                reader.Close();
            }

            List<string> relationshipNames = [];

            foreach (var model in parsedModels)
            {
                // Add edges based on the 'extends' field (especially needed for the 'IS_OF_MODEL' function)
                if (
                    model.Value is DTInterfaceInfo dTInterfaceInfo
                    && dTInterfaceInfo.Extends != null
                    && dTInterfaceInfo.Extends.Count > 0
                )
                {
                    // Get extends and create relationships
                    foreach (var extend in dTInterfaceInfo.Extends)
                    {
                        string extendsCypher =
                            $@"MATCH (m:Model), (m2:Model)
                        WHERE m.id = '{dTInterfaceInfo
                            .Id.AbsoluteUri}' AND m2.id = '{extend.Id.AbsoluteUri}'
                        CREATE (m)-[:_extends]->(m2)";
                        await using var extendsCommand = connection.CreateCypherCommand(
                            _graphName,
                            extendsCypher
                        );
                        // TODO: run these as batch commands
                        await extendsCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // Collect all relationship names so we can prepare the edge labels with replication full
                if (model.Value is DTRelationshipInfo dTRelationshipInfo)
                {
                    if (relationshipNames.Contains(dTRelationshipInfo.Name))
                    {
                        continue;
                    }
                    relationshipNames.Add(dTRelationshipInfo.Name);
                }
            }

            // Run create elabels and then set replication on the new table for each relationship name to ensure replication full
            // Make sure it doesn't fail if the elabel already exists
            foreach (var relationshipName in relationshipNames)
            {
                // Check if label already exists
                await using var labelExistsCommand = new NpgsqlCommand(
                    $@"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = '{_graphName}' AND table_name = '{relationshipName}');",
                    connection
                );
                if ((bool?)await labelExistsCommand.ExecuteScalarAsync(cancellationToken) == true)
                {
                    continue;
                }

                // Create new label
                await using var createElabelCommand = new NpgsqlCommand(
                    $@"SELECT create_elabel('{_graphName}', '{relationshipName}');",
                    connection
                );
                await createElabelCommand.ExecuteNonQueryAsync(cancellationToken);

                await using var setReplicationCommand = new NpgsqlCommand(
                    $@"ALTER TABLE {_graphName}.""{relationshipName}"" REPLICA IDENTITY FULL",
                    connection
                );
                await setReplicationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
        catch (PostgresException ex) when (ex.ConstraintName == "model_id_idx")
        {
            throw new ModelAlreadyExistsException(ex.Message);
        }
        catch (ParsingException ex)
        {
            throw new DTDLParserParsingException(ex);
        }
    }

    public virtual async Task DeleteModelAsync(
        string modelId,
        CancellationToken cancellationToken = default
    )
    {
        // TODO: should not be able to delete a model where other models extend from.
        string cypher =
            $@"
            MATCH (m:Model)
            WHERE m.id = '{modelId}' 
            OPTIONAL MATCH (m)-[r:_extends]-()
            DELETE r, m";
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.ReadWrite,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            throw new ModelNotFoundException($"Model with ID {modelId} not found");
        }
    }
}
