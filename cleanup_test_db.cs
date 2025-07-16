using Microsoft.Extensions.Configuration;
using Npgsql;
using Npgsql.Age;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.Development.json")
    .Build();

string connectionString =
    configuration.GetConnectionString("agedb") ?? throw new ArgumentNullException("agedb");

NpgsqlConnectionStringBuilder connectionStringBuilder =
    new(connectionString) { SearchPath = "ag_catalog, \"$user\", public" };
NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);

var dataSource = dataSourceBuilder.UseAge(true).BuildMultiHost();

try
{
    await using var connection = await dataSource.OpenConnectionAsync();

    // Find all job schemas
    var findSchemasCommand = new NpgsqlCommand(
        @"
        SELECT schemaname 
        FROM pg_catalog.pg_tables 
        WHERE schemaname LIKE '%_jobs'
        GROUP BY schemaname",
        connection
    );

    var schemas = new List<string>();
    await using var reader = await findSchemasCommand.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        schemas.Add(reader.GetString(0));
    }
    await reader.CloseAsync();

    Console.WriteLine($"Found {schemas.Count} job schemas:");
    foreach (var schema in schemas)
    {
        Console.WriteLine($"  - {schema}");
    }

    // Clean up all job schemas
    foreach (var schema in schemas)
    {
        var dropCommand = new NpgsqlCommand(
            $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",
            connection
        );
        await dropCommand.ExecuteNonQueryAsync();
        Console.WriteLine($"Dropped schema: {schema}");
    }

    Console.WriteLine("Database cleanup completed.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    await dataSource.DisposeAsync();
}
