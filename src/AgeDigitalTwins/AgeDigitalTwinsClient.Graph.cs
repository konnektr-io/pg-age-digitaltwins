using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Npgsql.Age;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    /// <summary>
    /// Initializes the graph by creating it if it does not exist.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("InitializeAsync", ActivityKind.Client);

        try
        {
            if (await GraphExistsAsync(cancellationToken) != true)
            {
                // When a new graph is created, it will also be initialized
                // with labels, indexes, functions, etc.
                await CreateGraphAsync(cancellationToken);
            }
            else
            {
                // Always make sure we are using the last version of all functions and indexes
                await GraphUpdateFunctionsAsync(cancellationToken);
            }
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
    /// Checks if the graph exists asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the graph exists.</returns>
    public virtual async Task<bool?> GraphExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.ReadWrite,
            cancellationToken
        );
        await using var command = connection.GraphExistsCommand(_graphName);
        return (bool?)await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the graph asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task CreateGraphAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("CreateGraphAsync", ActivityKind.Client);

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await using var command = connection.CreateGraphCommand(_graphName);
            await command.ExecuteNonQueryAsync(cancellationToken);

            using var batch = new NpgsqlBatch(connection);
            foreach (
                NpgsqlBatchCommand initBatchCommand in GraphInitialization.GetGraphInitCommands(
                    _graphName
                )
            )
            {
                batch.BatchCommands.Add(initBatchCommand);
            }
            await batch.ExecuteNonQueryAsync(cancellationToken);

            await GraphUpdateFunctionsAsync(cancellationToken);
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
    /// Drops the graph asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task DropGraphAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("DropGraphAsync", ActivityKind.Client);

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(
                TargetSessionAttributes.ReadWrite,
                cancellationToken
            );
            await using var command = connection.DropGraphCommand(_graphName);
            await command.ExecuteNonQueryAsync(cancellationToken);
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
    /// Initializes the graph by creating labels, indexes, functions, etc.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task GraphUpdateFunctionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.ReadWrite,
            cancellationToken
        );

        // Graph should already exist at this point
        // Create or Replace Functions to get the latest versions of the functions
        using var batch = new NpgsqlBatch(connection);
        foreach (
            NpgsqlBatchCommand initBatchCommand in GraphInitialization.GetGraphUpdateFunctionsCommands(
                _graphName
            )
        )
        {
            batch.BatchCommands.Add(initBatchCommand);
        }
        await batch.ExecuteNonQueryAsync(cancellationToken);
    }
}
