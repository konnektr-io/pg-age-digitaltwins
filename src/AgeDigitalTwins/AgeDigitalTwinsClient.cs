using System;
using System.Text.Json;
using System.Threading.Tasks;
using AgeDigitalTwins.Validation;
using DTDLParser;
using Npgsql;
using Npgsql.Age;

namespace AgeDigitalTwins;

public partial class AgeDigitalTwinsClient : IAsyncDisposable
{
    private readonly NpgsqlMultiHostDataSource _dataSource;

    private readonly string _graphName;

    private readonly ModelParser _modelParser;

    private readonly JsonSerializerOptions serializerOptions =
        new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public AgeDigitalTwinsClient(
        NpgsqlMultiHostDataSource dataSource,
        string graphName = "digitaltwins"
    )
    {
        _graphName = graphName;
        _dataSource = dataSource;
        _modelParser = new(
            new ParsingOptions()
            {
                MaxDtdlVersion = 4,
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(_graphName, dtmis, ct),
            }
        );
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    public AgeDigitalTwinsClient(string connectionString, string graphName = "digitaltwins")
    {
        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(connectionString) { SearchPath = "ag_catalog, \"$user\", public" };
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);
        _dataSource = dataSourceBuilder.UseAge(true).BuildMultiHost();

        _graphName = graphName;
        _modelParser = new(
            new ParsingOptions()
            {
                MaxDtdlVersion = 4,
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSource.ParserDtmiResolverAsync(_graphName, dtmis, ct),
            }
        );
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
