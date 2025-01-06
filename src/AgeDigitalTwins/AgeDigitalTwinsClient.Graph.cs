using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Npgsql.Age;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient
{
    public virtual async Task InitializeGraphAsync(CancellationToken cancellationToken = default)
    {
        if (await GraphExistsAsync(cancellationToken) != true)
        {
            await CreateGraphAsync(cancellationToken);
        }
    }

    public virtual async Task<bool?> GraphExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.GraphExistsCommand(_graphName);
        return (bool?)await command.ExecuteScalarAsync(cancellationToken);
    }

    public virtual async Task CreateGraphAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateGraphCommand(_graphName);
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Initialize the graph by creating labels, indexes, functions, ...
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
    }

    public virtual async Task DropGraphAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.DropGraphCommand(_graphName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
