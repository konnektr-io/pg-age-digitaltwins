using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgeDigitalTwins.Exceptions;
using AgeDigitalTwins.Models;
using DTDLParser;
using DTDLParser.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
    public virtual AsyncPageable<DigitalTwinsModelData?> GetModelsAsync(
        GetModelsOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new GetModelsOptions();

        string cypher;
        string returnStateMent = options.IncludeModelDefinition
            ? " RETURN *"
            : " RETURN m.id AS id, m.uploadTime AS uploadTime, m.displayName AS displayName, m.description AS description, m.decommissioned AS decommissioned, m.bases AS bases";

        if (options.DependenciesFor != null && options.DependenciesFor.Length > 0)
        {
            string dependenciesForList = $"['{string.Join("','", options.DependenciesFor)}']";
            cypher =
                $@"
MATCH (m:Model) WHERE m.id IN {dependenciesForList}
{returnStateMent}
UNION
UNWIND {dependenciesForList} AS modelId
MATCH (m1:Model {{id: modelId}})
UNWIND m1.bases AS dependency
MATCH (m:Model {{id: dependency}})
{returnStateMent}";
        }
        else
        {
            cypher = @"MATCH (m:Model)";
            cypher += returnStateMent;
        }

        return QueryAsync<DigitalTwinsModelData>(cypher, cancellationToken);
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
        GetModelOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("GetModelAsync", ActivityKind.Client);
        activity?.SetTag("modelId", modelId);

        try
        {
            // Fetch the main model first
            string mainCypher = $@"MATCH (m:Model {{id: '{modelId}'}}) RETURN m";
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.PreferStandby,
                cancellationToken
            );
            await using var command = connection.CreateCypherCommand(_graphName, mainCypher);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            DigitalTwinsModelData? mainModel = null;
            if (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                mainModel = new DigitalTwinsModelData(vertex.Properties);
            }
            else
            {
                throw new ModelNotFoundException($"Model with ID {modelId} not found");
            }
            reader.Close();

            if (mainModel == null)
            {
                throw new ModelNotFoundException($"Model with ID {modelId} not found");
            }

            if (options?.IncludeBaseModelContents == true)
            {
                // Helper to extract contents by type from DtdlModel
                List<JsonElement> ExtractContentsByType(
                    DigitalTwinsModelData model,
                    string typeName
                )
                {
                    var result = new List<JsonElement>();
                    if (model.DtdlModelJson.HasValue)
                    {
                        var dtdl = model.DtdlModelJson.Value;
                        JsonElement contents;
                        if (dtdl.TryGetProperty("contents", out contents))
                        {
                            if (contents.ValueKind == JsonValueKind.Object)
                            {
                                // Single content as object
                                if (ContentHasType(contents, typeName))
                                    result.Add(contents);
                            }
                            else if (contents.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in contents.EnumerateArray())
                                {
                                    if (ContentHasType(item, typeName))
                                        result.Add(item);
                                }
                            }
                        }
                    }
                    return result;
                }

                // Helper to check if a content has a type (handles string or array)
                bool ContentHasType(JsonElement content, string typeName)
                {
                    if (content.TryGetProperty("@type", out var typeProp))
                    {
                        if (typeProp.ValueKind == JsonValueKind.String)
                            return typeProp.GetString() == typeName;
                        if (typeProp.ValueKind == JsonValueKind.Array)
                            return typeProp.EnumerateArray().Any(e => e.GetString() == typeName);
                    }
                    return false;
                }

                // Helper to merge all contents of a given type from all models
                List<JsonElement>? MergeContents(
                    IEnumerable<DigitalTwinsModelData> models,
                    string typeName
                )
                {
                    var allContents = new List<JsonElement>();
                    foreach (var m in models)
                    {
                        allContents.AddRange(ExtractContentsByType(m, typeName));
                    }
                    if (allContents.Count == 0)
                        return null;
                    return allContents;
                }

                var allModels = new List<DigitalTwinsModelData> { mainModel };

                // If there are bases, fetch and add them
                if (mainModel.Bases != null && mainModel.Bases.Length > 0)
                {
                    string basesList = $"['{string.Join("','", mainModel.Bases)}']";
                    string cypher = $@"MATCH (m:Model) WHERE m.id IN {basesList} RETURN m";
                    await using var baseCommand = connection.CreateCypherCommand(
                        _graphName,
                        cypher
                    );
                    await using var baseReader = await baseCommand.ExecuteReaderAsync(
                        cancellationToken
                    );
                    var baseModels = new List<DigitalTwinsModelData>();
                    while (await baseReader.ReadAsync(cancellationToken))
                    {
                        var agResult = await baseReader.GetFieldValueAsync<Agtype?>(0);
                        var vertex = (Vertex)agResult;
                        baseModels.Add(new DigitalTwinsModelData(vertex.Properties));
                    }
                    baseReader.Close();
                    allModels.AddRange(baseModels);
                }

                mainModel.Properties = MergeContents(allModels, "Property");
                mainModel.Relationships = MergeContents(allModels, "Relationship");
                mainModel.Components = MergeContents(allModels, "Component");
                mainModel.Telemetries = MergeContents(allModels, "Telemetry");
                mainModel.Commands = MergeContents(allModels, "Command");
            }

            return mainModel;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "Exception",
                    default,
                    new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace },
                    }
                )
            );
            throw;
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
        using var activity = ActivitySource.StartActivity("CreateModelsAsync", ActivityKind.Client);
        activity?.SetTag("modelCount", dtdlModels.Count());

        try
        {
            var objectModel = await _modelParser.ParseAsync(
                ConvertToAsyncEnumerable(dtdlModels),
                cancellationToken: cancellationToken
            );

            // Dictionary to track descendants: key = base model ID, value = list of models that extend it
            var descendantsMap = new Dictionary<string, HashSet<string>>();

            IEnumerable<DigitalTwinsModelData> modelDatas = dtdlModels
                .Select(dtdlModel =>
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

                                // Track that current model is a descendant of this base
                                if (!descendantsMap.ContainsKey(extendedInterface.Id.AbsoluteUri))
                                {
                                    descendantsMap[extendedInterface.Id.AbsoluteUri] =
                                        new HashSet<string>();
                                }
                                descendantsMap[extendedInterface.Id.AbsoluteUri].Add(modelData.Id);

                                AddBaseInterfaces(extendedInterface); // Recursive call
                            }
                        }
                    }
                    // Add the base interfaces to the list of bases (recursively)
                    AddBaseInterfaces(interfaceInfo);
                    // Add the collected bases to the modelData
                    modelData.Bases = bases.ToArray();
                    return modelData;
                })
                .ToList(); // Materialize to ensure descendantsMap is fully populated

            // Now assign descendants to each model in the current batch
            foreach (var modelData in modelDatas)
            {
                if (descendantsMap.TryGetValue(modelData.Id, out var descendants))
                {
                    modelData.Descendants = descendants.ToArray();
                }
                else
                {
                    modelData.Descendants = Array.Empty<string>();
                }
            }

            // Identify base models that need descendants updates but aren't in current batch
            var currentModelIds = new HashSet<string>(modelDatas.Select(m => m.Id));
            var baseModelsToUpdate = descendantsMap
                .Keys.Where(baseModelId => !currentModelIds.Contains(baseModelId))
                .ToDictionary(
                    baseModelId => baseModelId,
                    baseModelId => descendantsMap[baseModelId]
                );

            // This is needed as after unwinding, it gets converted to agtype again
            string modelsString =
                $"['{string.Join("','", modelDatas.Select(m => JsonSerializer.Serialize(m, serializerOptions).Replace("'", "\\'")))}']";

            // It is not possible to update or overwrite an existing model
            // Trying so will raise a unique constraint violation
            string cypher =
                $@"UNWIND {modelsString} as model
WITH model::cstring::agtype as modelAgtype
CREATE (m:Model {{id: modelAgtype.id}})
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

            // Update descendants for existing base models that aren't in the current batch
            if (baseModelsToUpdate.Count > 0)
            {
                foreach (var (baseModelId, newDescendants) in baseModelsToUpdate)
                {
                    // Fetch current descendants from the database
                    string fetchCypher =
                        $@"
                        MATCH (m:Model)
                        WHERE m.id = '{baseModelId}'
                        RETURN m.descendants";

                    await using var fetchCommand = connection.CreateCypherCommand(
                        _graphName,
                        fetchCypher
                    );

                    HashSet<string> existingDescendants = new HashSet<string>();
                    await using (
                        var reader = await fetchCommand.ExecuteReaderAsync(cancellationToken)
                    )
                    {
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            var descendantsAgtype = await reader.GetFieldValueAsync<Agtype?>(
                                0,
                                cancellationToken
                            );
                            if (descendantsAgtype != null)
                            {
                                var descendantsList = descendantsAgtype.Value.GetList();
                                foreach (var desc in descendantsList)
                                {
                                    if (desc is string descStr)
                                    {
                                        existingDescendants.Add(descStr);
                                    }
                                }
                            }
                        }
                    } // Reader is disposed here, closing it before the update command

                    // Merge with new descendants
                    existingDescendants.UnionWith(newDescendants);

                    // Update the model with merged descendants
                    var mergedDescendantsJson = JsonSerializer.Serialize(
                        existingDescendants.ToArray(),
                        serializerOptions
                    );
                    string updateCypher =
                        $@"
                        MATCH (m:Model)
                        WHERE m.id = '{baseModelId}'
                        SET m.descendants = '{mergedDescendantsJson.Replace("'", "\\'")}'::cstring::agtype
                        RETURN m";

                    await using var updateCommand = connection.CreateCypherCommand(
                        _graphName,
                        updateCypher
                    );
                    await updateCommand.ExecuteNonQueryAsync(cancellationToken);
                }
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
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "Exception",
                    default,
                    new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace },
                    }
                )
            );
            throw;
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
        using var activity = ActivitySource.StartActivity("DeleteModelAsync", ActivityKind.Client);
        activity?.SetTag("modelId", modelId);

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
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "Exception",
                    default,
                    new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace },
                    }
                )
            );
            throw;
        }
    }

    /// <summary>
    /// Deletes all models asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<int> DeleteAllModelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "DeleteAllModelsAsync",
            ActivityKind.Client
        );

        try
        {
            // Delete all models and relationships
            string cypher = $@"MATCH (m:Model) DETACH DELETE m RETURN COUNT(m) AS deletedCount";
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
            return rowsAffected;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(
                new ActivityEvent(
                    "Exception",
                    default,
                    new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.StackTrace },
                    }
                )
            );
            throw;
        }
    }

    private async Task<DigitalTwinsModelData?> GetModelWithCacheAsync(
        string modelId,
        CancellationToken cancellationToken = default
    )
    {
        if (_modelCacheExpiration == TimeSpan.Zero)
        {
            // If cache expiration is set to zero, do not use the cache
            return await GetModelAsync(modelId, cancellationToken: cancellationToken);
        }
        return await _modelCache.GetOrCreateAsync(
            modelId,
            async entry =>
            {
                entry.SetOptions(
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _modelCacheExpiration,
                    }
                );

                return await GetModelAsync(modelId, cancellationToken: cancellationToken);
            }
        );
    }

    /// <summary>
    /// Gets the model ID for a digital twin with caching.
    /// </summary>
    /// <param name="twinId">The ID of the digital twin.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The model ID of the digital twin.</returns>
    /// <exception cref="DigitalTwinNotFoundException">Thrown when the digital twin is not found.</exception>
    /// <exception cref="ValidationFailedException">Thrown when the digital twin doesn't have valid metadata.</exception>
    private async Task<string> GetModelIdByTwinIdCachedAsync(
        string twinId,
        CancellationToken cancellationToken = default
    )
    {
        if (_modelCacheExpiration == TimeSpan.Zero)
        {
            // If cache expiration is set to zero, do not use the cache
            return await GetModelIdByTwinIdAsync(twinId, cancellationToken);
        }

        string cacheKey = $"twin_model:{twinId}";
        return await _modelCache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.SetOptions(
                        new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = _modelCacheExpiration,
                        }
                    );

                    return await GetModelIdByTwinIdAsync(twinId, cancellationToken);
                }
            ) ?? throw new DigitalTwinNotFoundException($"Digital Twin with ID {twinId} not found");
    }

    /// <summary>
    /// Gets the model ID for a digital twin without caching.
    /// </summary>
    /// <param name="twinId">The ID of the digital twin.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The model ID of the digital twin.</returns>
    /// <exception cref="DigitalTwinNotFoundException">Thrown when the digital twin is not found.</exception>
    /// <exception cref="ValidationFailedException">Thrown when the digital twin doesn't have valid metadata.</exception>
    private async Task<string> GetModelIdByTwinIdAsync(
        string twinId,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );

        string cypher =
            $@"MATCH (t:Twin {{`$dtId`: '{twinId.Replace("'", "\\'")}'}}) 
                          RETURN t.`$metadata`.`$model` as modelId";

        await using var command = connection.CreateCypherCommand(_graphName, cypher);

        var modelIdValue = await command.ExecuteScalarAsync(cancellationToken);

        if (modelIdValue == null || modelIdValue == DBNull.Value)
        {
            throw new ValidationFailedException(
                $"Digital Twin '{twinId}' does not have a valid model ID in its metadata"
            );
        }

        // Handle AGE type conversion to string
        string? modelId = ((Agtype)modelIdValue).GetString().Trim('\u0001').Trim('"');

        if (string.IsNullOrEmpty(modelId))
        {
            throw new ValidationFailedException(
                $"Digital Twin '{twinId}' has an empty or null model ID"
            );
        }

        return modelId;
    }

    /// <summary>
    /// Retrieves a model with its schema flattened (including inherited properties, relationships, and components).
    /// </summary>
    /// <param name="modelId">The ID of the model to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary representing the flattened model schema.</returns>
    public async Task<Dictionary<string, object>> GetModelExpandedAsync(
        string modelId,
        CancellationToken cancellationToken = default
    )
    {
        var dtmi = new Dtmi(modelId);
        var parsedModel = await _modelParser.ParseAsync(
            ConvertToAsyncEnumerable(new[] { modelId }),
            cancellationToken: cancellationToken
        );

        var targetInterface =
            parsedModel.Values.OfType<DTInterfaceInfo>().FirstOrDefault(i => i.Id == dtmi)
            ?? throw new ModelNotFoundException(
                $"Model {modelId} not found or is not an interface."
            );

        var properties = new Dictionary<string, object>();
        var relationships = new Dictionary<string, object>();
        var components = new Dictionary<string, object>();

        // Helper to collect contents recursively
        void CollectContents(DTInterfaceInfo iface)
        {
            // Traverse parents first to allow overriding (though DTDL usually forbids shadowing, this establishes order)
            foreach (var parent in iface.Extends)
            {
                CollectContents(parent);
            }

            foreach (var content in iface.Contents.Values)
            {
                if (content is DTPropertyInfo prop)
                {
                    properties[prop.Name] = new
                    {
                        name = prop.Name,
                        schema = prop.Schema.Id.AbsoluteUri,
                        description = prop.Description.Values.FirstOrDefault() ?? "",
                        writable = prop.Writable,
                    };
                }
                else if (content is DTRelationshipInfo rel)
                {
                    relationships[rel.Name] = new
                    {
                        name = rel.Name,
                        target = rel.Target?.AbsoluteUri,
                        description = rel.Description.Values.FirstOrDefault() ?? "",
                        properties = rel.Properties.ToDictionary(
                            p => p.Name,
                            p => new { schema = p.Schema.Id.AbsoluteUri }
                        ),
                    };
                }
                else if (content is DTComponentInfo comp)
                {
                    components[comp.Name] = new
                    {
                        name = comp.Name,
                        schema = comp.Schema.Id.AbsoluteUri,
                        description = comp.Description.Values.FirstOrDefault() ?? "",
                    };
                }
            }
        }

        CollectContents(targetInterface);

        return new Dictionary<string, object>
        {
            ["id"] = targetInterface.Id.AbsoluteUri,
            ["displayName"] = targetInterface.DisplayName.Values.FirstOrDefault() ?? "",
            ["description"] = targetInterface.Description.Values.FirstOrDefault() ?? "",
            ["properties"] = properties,
            ["relationships"] = relationships,
            ["components"] = components,
        };
    }

    /// <summary>
    /// Updates the embedding for a specific model asynchronously.
    /// </summary>
    /// <param name="modelId">The ID of the model to update.</param>
    /// <param name="embedding">The vector embedding.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public virtual async Task UpdateModelEmbeddingAsync(
        string modelId,
        double[] embedding,
        CancellationToken cancellationToken = default
    )
    {
        string vectorString = JsonSerializer.Serialize(embedding);
        string cypher =
            $@"MATCH (m:Model {{id: '{modelId}'}}) SET m.embedding = {vectorString}::vector";

        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.ReadWrite,
            cancellationToken
        );
        await using var command = connection.CreateCypherCommand(_graphName, cypher);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Searches for models using both vector similarity and lexical search.
    /// </summary>
    /// <param name="query">The search query string (lexical).</param>
    /// <param name="vector">The search vector (optional).</param>
    /// <param name="limit">Max results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of matching DigitalTwinsModelData.</returns>
    public virtual async Task<IEnumerable<DigitalTwinsModelData>> SearchModelsAsync(
        string? query,
        double[]? vector,
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(query) && vector == null)
        {
            // If no criteria, regular list with limit
            var models = new List<DigitalTwinsModelData>();
            await foreach (var m in GetModelsAsync(cancellationToken: cancellationToken))
            {
                if (models.Count >= limit)
                    break;
                if (m != null)
                    models.Add(m);
            }
            return models;
        }

        string cypher;
        if (vector != null)
        {
            string vectorString = JsonSerializer.Serialize(vector);
            string whereClause = !string.IsNullOrWhiteSpace(query)
                ? $" WHERE (toLower(toString(m.displayName)) CONTAINS toLower('{query.Replace("'", "\\'")}') OR toLower(toString(m.description)) CONTAINS toLower('{query.Replace("'", "\\'")}') OR toLower(m.id) CONTAINS toLower('{query.Replace("'", "\\'")}' )) "
                : "";

            // Hybrid: Vector + Filter
            cypher =
                $@"
                MATCH (m:Model)
                {whereClause}
                RETURN m
                ORDER BY l2_distance(m.embedding, {vectorString}::vector) ASC
                LIMIT {limit}";
        }
        else
        {
            // Lexical only
            // Using CONTAINS (case-insensitive simulation via toLower)
            // Note: m.displayName and m.description are maps, so toString(m.displayName) might result in valid JSON string which contains the value.
            // Ideally we should look into specific language values, but generic string check is a good approximation for 'CONTAINS'.
            string q = query!.Replace("'", "\\'");
            cypher =
                $@"
                MATCH (m:Model)
                WHERE toLower(toString(m.displayName)) CONTAINS toLower('{q}') 
                   OR toLower(toString(m.description)) CONTAINS toLower('{q}')
                   OR toLower(m.id) CONTAINS toLower('{q}')
                RETURN m
                LIMIT {limit}";
        }

        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.PreferStandby,
            cancellationToken
        );

        await using var command = connection.CreateCypherCommand(_graphName, cypher);

        var results = new List<DigitalTwinsModelData>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var agResult = await reader.GetFieldValueAsync<Agtype?>(0);
                var vertex = (Vertex)agResult;
                results.Add(new DigitalTwinsModelData(vertex.Properties));
            }
        }
        catch (Exception)
        {
            // Fallback or rethrow?
            // If vector search fails (e.g., extensions not installed), we might bubble it up.
            throw;
        }
        return results;
    }
}
