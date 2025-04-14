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
    /// <summary>
    /// Converts an enumerable of strings to an asynchronous enumerable.
    /// </summary>
    /// <param name="source">The source enumerable of strings.</param>
    /// <returns>An asynchronous enumerable of strings.</returns>
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

    /// <summary>
    /// Retrieves models asynchronously based on the provided options.
    /// </summary>
    /// <param name="options">Options to filter and include model definitions.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous enumerable of <see cref="DigitalTwinsModelData"/>.</returns>
    public virtual async IAsyncEnumerable<DigitalTwinsModelData> GetModelsAsync(
        GetModelsOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        options ??= new GetModelsOptions();

        string cypher;

        if (options.DependenciesFor != null && options.DependenciesFor.Length > 0)
        {
            string dependenciesForList = $"['{string.Join("','", options.DependenciesFor)}']";
            cypher =
                $@"
                UNWIND {dependenciesForList} AS modelId
                MATCH (m:Model {{id: modelId}})
                UNWIND m.bases AS dependency
                MATCH (m2:Model {{id: dependency}})
                RETURN m2 AS model";
        }
        else
        {
            cypher = @"MATCH (m:Model)";
        }

        if (options.IncludeModelDefinition)
        {
            cypher += "RETURN m";
        }
        else
        {
            cypher +=
                @"RETURN {id:m.id, uploadTime: m.uploadTime, displayName: m.displayName, description: m.description, decommissioned: m.decommissioned} AS m";
        }

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

    /// <summary>
    /// Retrieves a specific model asynchronously by its ID.
    /// </summary>
    /// <param name="modelId">The ID of the model to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The retrieved <see cref="DigitalTwinsModelData"/>.</returns>
    /// <exception cref="ModelNotFoundException">Thrown when the model with the specified ID is not found.</exception>
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

    /// <summary>
    /// Creates models asynchronously from the provided DTDL models.
    /// </summary>
    /// <param name="dtdlModels">The DTDL models to create.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of created <see cref="DigitalTwinsModelData"/>.</returns>
    /// <exception cref="ModelAlreadyExistsException">Thrown when a model with the same ID already exists.</exception>
    /// <exception cref="DTDLParserParsingException">Thrown when there is an error parsing the DTDL models.</exception>
    public virtual async Task<IReadOnlyList<DigitalTwinsModelData>> CreateModelsAsync(
        IEnumerable<string> dtdlModels,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var objectModel = await _modelParser.ParseAsync(
                ConvertToAsyncEnumerable(dtdlModels),
                cancellationToken: cancellationToken
            );
            IEnumerable<DigitalTwinsModelData> modelDatas = dtdlModels.Select(dtdlModel =>
            {
                // Prepare the bases array to store all bases (dtmis that the interface extends from)
                var bases = new List<string>();
                // Parse the original json and prepare the modelData object
                var modelData = new DigitalTwinsModelData(dtdlModel);
                // Find the interface from the objectModel dictionary using the modelId
                var interfaceInfo = (DTInterfaceInfo)objectModel[new Dtmi(modelData.Id)];
                // Recursively add all base interfaces to the list of bases
                void AddBaseInterfaces(DTInterfaceInfo currentInterface)
                {
                    foreach (DTInterfaceInfo extendedInterface in currentInterface.Extends)
                    {
                        if (!bases.Contains(extendedInterface.Id.AbsoluteUri))
                        {
                            bases.Add(extendedInterface.Id.AbsoluteUri);
                            AddBaseInterfaces(extendedInterface); // Recursive call
                        }
                    }
                }
                // Add the base interfaces to the list of bases (recursively)
                AddBaseInterfaces(interfaceInfo);
                // Add the collected bases to the modelData
                modelData.Bases = bases.ToArray();
                return modelData;
            });
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

            foreach (var entityInfoKv in objectModel)
            {
                if (entityInfoKv.Value is DTInterfaceInfo dTInterfaceInfo)
                {
                    // Add edges for dependencies based on the 'extends' field
                    if (dTInterfaceInfo.Extends != null && dTInterfaceInfo.Extends.Count > 0)
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

                    // Add edges for dependencies based on the 'schema' field of components
                    if (dTInterfaceInfo.Components != null && dTInterfaceInfo.Components.Count > 0)
                    {
                        foreach (var component in dTInterfaceInfo.Components.Values)
                        {
                            if (component.Schema != null && component.Schema.Id != null)
                            {
                                string hasComponentCypher =
                                    $@"MATCH (m:Model), (m2:Model)
                                    WHERE m.id = '{dTInterfaceInfo
                                    .Id.AbsoluteUri}' AND m2.id = '{component.Schema.Id.AbsoluteUri}'
                                    CREATE (m)-[:_hasComponent]->(m2)";

                                await using var hasComponentCommand =
                                    connection.CreateCypherCommand(_graphName, hasComponentCypher);
                                await hasComponentCommand.ExecuteNonQueryAsync(cancellationToken);
                            }
                        }
                    }
                }

                // Collect all relationship names so we can prepare the edge labels with replication full
                if (entityInfoKv.Value is DTRelationshipInfo dTRelationshipInfo)
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

                // Set the replication identity to FULL for the new label (to ensure all properties are replicated)
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

    /// <summary>
    /// Deletes a model asynchronously by its ID.
    /// </summary>
    /// <param name="modelId">The ID of the model to delete.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ModelNotFoundException">Thrown when the model with the specified ID is not found.</exception>
    /// <exception cref="ModelReferencesNotDeletedException">Thrown when the model has references that are not deleted.</exception>
    public virtual async Task DeleteModelAsync(
        string modelId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Delete the model and outgoing relationships
            // If there are any other relationships left (dependencies), the query should fail
            string cypher =
                $@"MATCH (m:Model {{id: '{modelId}'}})
                OPTIONAL MATCH (m)-[r]->(:Model)
                DELETE r, m
                RETURN COUNT(m) AS deletedCount";
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await using var command = connection.CreateCypherCommand(_graphName, cypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            int rowsAffected = 0;
            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
                rowsAffected = (int)agResult;
            }
            if (rowsAffected <= 0)
            {
                throw new ModelNotFoundException($"Model with ID {modelId} not found");
            }
        }
        catch (PostgresException ex) when (ex.Routine == "check_for_connected_edges")
        {
            throw new ModelReferencesNotDeletedException();
        }
    }

    /// <summary>
    /// Deletes all models asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task DeleteAllModelsAsync(CancellationToken cancellationToken = default)
    {
        // Delete all models and relationships
        string cypher =
            $@"MATCH (m:Model)
            DETACH DELETE m
            RETURN COUNT(m) AS deletedCount";
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.ReadWrite,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        int rowsAffected = 0;
        if (await reader.ReadAsync(cancellationToken))
        {
            var agResult = await reader.GetFieldValueAsync<Agtype?>(0).ConfigureAwait(false);
            rowsAffected = (int)agResult;
        }
        if (rowsAffected <= 0)
        {
            throw new ModelNotFoundException($"No models found");
        }
    }
}
