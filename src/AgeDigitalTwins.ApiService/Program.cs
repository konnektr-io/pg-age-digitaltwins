using System.Text.Json;
using AgeDigitalTwins;
using Json.Patch;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

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

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async httpContext =>
    {
        var exceptionHandlerPathFeature =
            httpContext.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;
        if (exception != null)
        {
            switch (exception)
            {
                case AgeDigitalTwins.Exceptions.ModelNotFoundException:
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await httpContext.Response.WriteAsync("Model not found.");
                    break;
                case AgeDigitalTwins.Exceptions.DigitalTwinNotFoundException:
                    httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    await httpContext.Response.WriteAsync("Digital twin not found.");
                    break;
                case AgeDigitalTwins.Exceptions.ValidationFailedException:
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await httpContext.Response.WriteAsync("Validation failed.");
                    break;
                case AgeDigitalTwins.Exceptions.InvalidAdtQueryException:
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await httpContext.Response.WriteAsync("Invalid ADT query.");
                    break;
                // Add more cases for other custom exceptions as needed
                default:
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await httpContext.Response.WriteAsync($"An error occurred: {exception.Message}");
                    break;
            }
            return;
        }
        var pds = httpContext.RequestServices.GetService<IProblemDetailsService>();
        if (pds == null
            || !await pds.TryWriteAsync(new() { HttpContext = httpContext }))
        {
            // Fallback behavior
            await httpContext.Response.WriteAsync("Fallback: An error occurred.");
        }
    });
});

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

app.MapPost("/query", (string query, AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.QueryAsync<JsonDocument>(query, cancellationToken);
})
.WithName("Query");

app.MapPost("/models", (IEnumerable<string> models, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    return client.CreateModelsAsync(models, cancellationToken);
})
.WithName("CreateModels");

app.MapDefaultEndpoints();
//  app.UseHsts();

app.Run();