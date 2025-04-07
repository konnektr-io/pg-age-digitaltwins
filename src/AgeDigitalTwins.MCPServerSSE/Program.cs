using AgeDigitalTwins;
using Npgsql;
using Npgsql.Age;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServer().WithToolsFromAssembly();

// Add Npgsql data source with custom settings.
builder.AddKeyedNpgsqlDataSource(
    "agedb",
    configureSettings: settings =>
    {
        settings.DisableTracing = true;
        settings.ConnectionString ??=
            builder.Configuration.GetConnectionString("agedb")
            ?? builder.Configuration["ConnectionStrings:agedb"]
            ?? builder.Configuration["AgeConnectionString"]
            ?? throw new InvalidOperationException("Connection string is required.");
        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(settings.ConnectionString)
            {
                SearchPath = "ag_catalog, \"$user\", public",
                ConnectionIdleLifetime = 60,
                ConnectionLifetime = 300,
            };
        settings.ConnectionString = connectionStringBuilder.ConnectionString;
    },
    configureDataSourceBuilder: dataSourceBuilder =>
    {
        dataSourceBuilder.UseAge(true);
    }
);

// Read-only data source
builder.AddKeyedNpgsqlDataSource(
    "agedb_ro",
    configureSettings: settings =>
    {
        settings.DisableTracing = true;
        settings.ConnectionString ??=
            builder.Configuration.GetConnectionString("agedb_ro")
            ?? builder.Configuration["ConnectionStrings:agedb_ro"];
        if (settings.ConnectionString != null)
        {
            NpgsqlConnectionStringBuilder connectionStringBuilder =
                new(settings.ConnectionString)
                {
                    SearchPath = "ag_catalog, \"$user\", public",
                    ConnectionIdleLifetime = 60,
                    ConnectionLifetime = 300,
                };
            settings.ConnectionString = connectionStringBuilder.ConnectionString;
        }
    },
    configureDataSourceBuilder: dataSourceBuilder =>
    {
        dataSourceBuilder.UseAge(true);
    }
);

// Add AgeDigitalTwinsClient
builder.Services.AddSingleton(sp =>
{
    NpgsqlDataSource dataSourceRw = sp.GetRequiredKeyedService<NpgsqlDataSource>("agedb");
    NpgsqlDataSource? dataSourceRo = sp.GetKeyedService<NpgsqlDataSource>("agedb_ro");
    string graphName =
        builder.Configuration.GetSection("Parameters")["AgeGraphName"]
        ?? builder.Configuration["Parameters:AgeGraphName"]
        ?? builder.Configuration["AgeGraphName"]
        ?? "digitaltwins";
    Console.WriteLine($"Using graph: {graphName}");
    return new AgeDigitalTwinsClient(dataSourceRw, dataSourceRo, graphName);
});

var app = builder.Build();
app.MapMcp();
app.Run();
