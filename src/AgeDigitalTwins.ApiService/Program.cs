using System.Text.Json;
using AgeDigitalTwins;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure and register AgeDigitalTwinsClient
builder.Services.AddSingleton<AgeDigitalTwinsClient>(serviceProvider =>
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.Development.json")
        .Build();

    string connectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        ?? configuration.GetConnectionString("AgeConnectionString")
        ?? throw new Exception("Connection string not defined.");

    var graphName = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        ?? configuration.GetConnectionString("AgeGraphName")
        ?? "digitaltwins";
    var client = new AgeDigitalTwinsClient(connectionString, new() { GraphName = graphName });
    bool? graphExists = client.GraphExistsAsync().GetAwaiter().GetResult();
    if (graphExists == false)
    {
        client.CreateGraphAsync().GetAwaiter().GetResult();
    }
    return client;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/digitaltwins/{id}", (string id, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.GetDigitalTwinAsync<JsonDocument>(id, cancellationToken);
})
.WithName("GetDigitalTwin");

app.MapPut("/digitaltwins/{id}", (string id, JsonDocument digitalTwin, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.CreateOrReplaceDigitalTwinAsync(id, digitalTwin, cancellationToken);
})
.WithName("CreateOrReplaceDigitalTwin");

app.MapDelete("/digitaltwins/{id}", (string id, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.DeleteDigitalTwinAsync(id, cancellationToken);
});

app.MapGet("/digitaltwins/{id}/relationships/{relationshipId}", (string id, string relationshipId, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.GetRelationshipAsync<JsonDocument>(id, relationshipId, cancellationToken);
});

app.MapDefaultEndpoints();

app.Run();