using System.Text.Json;
using AgeDigitalTwins;
using Json.Patch;
using Microsoft.AspNetCore.Diagnostics;

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async httpContext =>
    {
        var pds = httpContext.RequestServices.GetService<IProblemDetailsService>();
        if (pds == null
            || !await pds.TryWriteAsync(new() { HttpContext = httpContext }))
        {
            var exceptionHandlerPathFeature = httpContext.Features.Get<IExceptionHandlerPathFeature>();
            // Fallback behavior
            await httpContext.Response.WriteAsync($"Fallback: An error occurred: {exceptionHandlerPathFeature?.Error.Message}");
        }
    });
});

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

app.MapPatch("digitaltwins/{id}", (string id, JsonPatch patch, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.UpdateDigitalTwinAsync(id, patch, cancellationToken);
})
.WithName("UpdateDigitalTwin");

app.MapDelete("/digitaltwins/{id}", (string id, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.DeleteDigitalTwinAsync(id, cancellationToken);
})
.WithName("DeleteDigitalTwin");

app.MapGet("/digitaltwins/{id}/incomingrelationships", (string id, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.GetIncomingRelationshipsAsync<JsonDocument>(id, cancellationToken);
})
.WithName("ListIncomingRelationships");

app.MapGet("/digitaltwins/{id}/relationships", (string id, HttpContext httpContext, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    string? relationshipName = httpContext.Request.Query["relationshipName"];
    return client.GetRelationshipsAsync<JsonDocument>(id, relationshipName, cancellationToken);
})
.WithName("ListRelationships");

app.MapGet("/digitaltwins/{id}/relationships/{relationshipId}", (string id, string relationshipId, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.GetRelationshipAsync<JsonDocument>(id, relationshipId, cancellationToken);
})
.WithName("GetRelationship");

app.MapPut("/digitaltwins/{id}/relationships/{relationshipId}", (string id, string relationshipId, JsonDocument relationship, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.CreateOrReplaceRelationshipAsync(id, relationshipId, relationship, cancellationToken);
})
.WithName("CreateOrReplaceRelationship");

app.MapPost("/query", (string query, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.QueryAsync<JsonDocument>(query, cancellationToken);
})
.WithName("Query");

app.MapDefaultEndpoints();
app.UseHsts();

app.Run();