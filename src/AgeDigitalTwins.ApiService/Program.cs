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
        ?? throw new ArgumentNullException("AgeConnectionString");

    var graphName = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        ?? configuration.GetConnectionString("AgeGraphName")
        ?? "digitaltwins";
    var client = new AgeDigitalTwinsClient(connectionString, new() { GraphName = graphName });
    client.CreateGraphAsync().GetAwaiter().GetResult();
    return client;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/digitaltwins/{id}", (string id, AgeDigitalTwinsClient client) =>
{
    return client.GetDigitalTwinAsync<JsonDocument>(id);
})
.WithName("GetDigitalTwin");

app.MapPut("/digitaltwins/{id}", (string id, JsonDocument digitalTwin, AgeDigitalTwinsClient client) =>
{
    return client.CreateOrReplaceDigitalTwinAsync(id, digitalTwin);
})
.WithName("CreateOrReplaceDigitalTwin");

app.MapDefaultEndpoints();

app.Run();