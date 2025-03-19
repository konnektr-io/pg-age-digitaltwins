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
    private readonly NpgsqlDataSource _dataSourceRw;
    private readonly NpgsqlDataSource? _dataSourceRo;

    private readonly string _graphName;

    private readonly ModelParser _modelParser;

    private readonly JsonSerializerOptions serializerOptions =
        new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public AgeDigitalTwinsClient(
        NpgsqlDataSource dataSourceRw,
        NpgsqlDataSource? dataSourceRo = null,
        string graphName = "digitaltwins"
    )
    {
        _graphName = graphName;
        _dataSourceRw = dataSourceRw;
        _dataSourceRo = dataSourceRo;
        _modelParser = new(
            new ParsingOptions()
            {
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSourceRw.ParserDtmiResolverAsync(_graphName, dtmis, ct),
            }
        );
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    public AgeDigitalTwinsClient(
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        string graphName = "digitaltwins"
    )
    {
        connectionStringBuilder.SearchPath = "ag_catalog, \"$user\", public";
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);
        _dataSourceRw = dataSourceBuilder.UseAge(true).Build();

        _graphName = graphName;
        _modelParser = new(
            new ParsingOptions()
            {
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSourceRw.ParserDtmiResolverAsync(_graphName, dtmis, ct),
            }
        );
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    public AgeDigitalTwinsClient(string connectionString, string graphName = "digitaltwins")
    {
        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(connectionString) { SearchPath = "ag_catalog, \"$user\", public" };
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionStringBuilder.ConnectionString);
        _dataSourceRw = dataSourceBuilder.UseAge(true).Build();

        _graphName = graphName;
        _modelParser = new(
            new ParsingOptions()
            {
                DtmiResolverAsync = (dtmis, ct) =>
                    _dataSourceRw.ParserDtmiResolverAsync(_graphName, dtmis, ct),
            }
        );
        InitializeGraphAsync().GetAwaiter().GetResult();
    }

    private NpgsqlDataSource GetDataSource(bool readOnly)
    {
        return readOnly && _dataSourceRo != null ? _dataSourceRo : _dataSourceRw;
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSourceRw.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
