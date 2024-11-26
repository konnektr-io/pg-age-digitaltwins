using System.Text.Json;
using AgeDigitalTwins;
using AgeDigitalTwins.ApiService;
using Json.Patch;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure and register AgeDigitalTwinsClient
builder.Services.AddSingleton(serviceProvider =>
{
    string connectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        ?? throw new Exception("Connection string not defined.");

    var graphName = Environment.GetEnvironmentVariable("AGE_GRAPH_NAME")
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

app.UseExceptionHandler();

app.MapGet("/digitaltwins/{id}", (string id, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.GetDigitalTwinAsync<JsonDocument>(id, cancellationToken);
})
.WithName("GetDigitalTwin");

app.MapPut("/digitaltwins/{id}", (string id, JsonDocument digitalTwin, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.CreateOrReplaceDigitalTwinAsync(id, digitalTwin, cancellationToken);
})
.WithName("CreateOrReplaceDigitalTwin");

app.MapPatch("digitaltwins/{id}", (string id, JsonPatch patch, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.UpdateDigitalTwinAsync(id, patch, cancellationToken);
})
.WithName("UpdateDigitalTwin");

app.MapDelete("/digitaltwins/{id}", (string id, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.DeleteDigitalTwinAsync(id, cancellationToken);
})
.WithName("DeleteDigitalTwin");

app.MapGet("/digitaltwins/{id}/incomingrelationships", (string id, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.GetIncomingRelationshipsAsync<JsonDocument>(id, cancellationToken);
})
.WithName("ListIncomingRelationships");

app.MapGet("/digitaltwins/{id}/relationships", (string id, HttpContext httpContext, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    string? relationshipName = httpContext.Request.Query["relationshipName"];
    return client.GetRelationshipsAsync<JsonDocument>(id, relationshipName, cancellationToken);
})
.WithName("ListRelationships");

app.MapGet("/digitaltwins/{id}/relationships/{relationshipId}", (string id, string relationshipId, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.GetRelationshipAsync<JsonDocument>(id, relationshipId, cancellationToken);
})
.WithName("GetRelationship");

app.MapPut("/digitaltwins/{id}/relationships/{relationshipId}", (string id, string relationshipId, JsonDocument relationship, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.CreateOrReplaceRelationshipAsync(id, relationshipId, relationship, cancellationToken);
})
.WithName("CreateOrReplaceRelationship");

app.MapPost("/query", async (string query, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return Results.Json(new { value = await client.QueryAsync<JsonDocument>(query, cancellationToken).ToListAsync(cancellationToken) });
})
.WithName("Query");

app.MapGet("/models", async ([FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return Results.Json(new { value = await client.GetModelsAsync(cancellationToken).ToListAsync(cancellationToken) });
})
.WithName("ListModels");

app.MapPost("/models", (JsonElement[] models, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.CreateModelsAsync(models.Select(m => m.GetRawText()), cancellationToken);
})
.WithName("CreateModels");

app.MapDelete("/models/{id}", (string id, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.DeleteModelAsync(id, cancellationToken);
})
.WithName("DeleteModel");

// This endpoint is only used for cleanup in tests
// When the client is initiated, a new graph will automatically be created if the specified graph doesn't exist
// Creating and dropping graphs should be done in the control plane
if (app.Environment.IsDevelopment())
{
    app.MapDelete("graph/delete", ([FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
    {
        return client.DropGraphAsync(cancellationToken);
    });
}

app.MapDefaultEndpoints();
//  app.UseHsts();

app.Run();