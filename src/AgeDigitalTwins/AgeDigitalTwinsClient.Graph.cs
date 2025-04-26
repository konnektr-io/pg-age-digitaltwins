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
    public virtual async Task InitializeGraphAsync(CancellationToken cancellationToken = default)
    {
        if (await GraphExistsAsync(cancellationToken) != true)
        {
            await CreateGraphAsync(cancellationToken);
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
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.ReadWrite,
            cancellationToken
        );
        await using var command = connection.CreateGraphCommand(_graphName);
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Initialize the graph by creating labels, indexes, functions, ...
        using var batch = new NpgsqlBatch(connection);
        foreach (
            NpgsqlBatchCommand initBatchCommand in Initialization.GetGraphInitCommands(_graphName)
        )
        {
            batch.BatchCommands.Add(initBatchCommand);
        }
        await batch.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Drops the graph asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task DropGraphAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(
            TargetSessionAttributes.ReadWrite,
            cancellationToken
        );
        await using var command = connection.DropGraphCommand(_graphName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
