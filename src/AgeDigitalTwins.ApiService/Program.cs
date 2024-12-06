using System.Text.Json;
using AgeDigitalTwins;
using AgeDigitalTwins.ApiService;
using Json.Patch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Npgsql;
using Npgsql.Age;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Npgsql data source with custom settings.
builder.AddNpgsqlDataSource(
    "agedb",
    configureSettings: settings =>
    {
        settings.DisableTracing = true;
        if (settings.ConnectionString == null)
        {
            settings.ConnectionString = builder.Configuration.GetConnectionString("agedb")
                ?? builder.Configuration["ConnectionStrings:agedb"]
                ?? builder.Configuration["AgeConnectionString"]
                ?? throw new InvalidOperationException("Connection string is required.");
        }
        NpgsqlConnectionStringBuilder connectionStringBuilder = new(settings.ConnectionString)
        {
            SearchPath = "ag_catalog, \"$user\", public",
            ConnectionIdleLifetime = 60,
            ConnectionLifetime = 300,
        };
        settings.ConnectionString = connectionStringBuilder.ConnectionString;
    },
    configureDataSourceBuilder: dataSourceBuilder =>
    {
        dataSourceBuilder.UseAge(false);
    }
);
// Enable OpenTelemetry tracing with Npgsql integration (does not work when having it enabled in Aspire.Npgsql)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder.AddNpgsql();
    });

// Add AgeDigitalTwinsClient
builder.Services.AddSingleton(sp =>
{
    NpgsqlDataSource dataSource = sp.GetRequiredService<NpgsqlDataSource>();
    string graphName = builder.Configuration.GetSection("Parameters")["AgeGraphName"]
        ?? builder.Configuration["Parameters:AgeGraphName"]
        ?? builder.Configuration["AgeGraphName"]
        ?? "digitaltwins";
    Console.WriteLine($"Using graph: {graphName}");
    return new AgeDigitalTwinsClient(dataSource, graphName);
});

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add authentication
// builder.Services.AddAuthentication().AddOpenIdConnect();

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

app.MapPut("/digitaltwins/{id}", (string id, JsonDocument digitalTwin, HttpContext httpContext, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    string? etag = null;
    if (httpContext.Request.Headers.TryGetValue("If-None-Match", out StringValues etagValues) && etagValues.Count > 0)
    {
        etag = etagValues[0];
    }
    return client.CreateOrReplaceDigitalTwinAsync(id, digitalTwin, etag, cancellationToken);
})
.WithName("CreateOrReplaceDigitalTwin");

app.MapPatch("digitaltwins/{id}", async (string id, JsonPatch patch, HttpContext httpContext, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    string? etag = null;
    if (httpContext.Request.Headers.TryGetValue("If-Match", out StringValues etagValues) && etagValues.Count > 0)
    {
        etag = etagValues[0];
    }
    await client.UpdateDigitalTwinAsync(id, patch, etag, cancellationToken);
    return Results.NoContent();
})
.WithName("UpdateDigitalTwin");

app.MapDelete("/digitaltwins/{id}", async (string id, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    await client.DeleteDigitalTwinAsync(id, cancellationToken);
    return Results.NoContent();
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

app.MapPut("/digitaltwins/{id}/relationships/{relationshipId}", (string id, string relationshipId, JsonDocument relationship, HttpContext httpContext, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    string? etag = null;
    if (httpContext.Request.Headers.TryGetValue("If-None-Match", out StringValues etagValues) && etagValues.Count > 0)
    {
        etag = etagValues[0];
    }
    return client.CreateOrReplaceRelationshipAsync(id, relationshipId, relationship, etag, cancellationToken);
})
.WithName("CreateOrReplaceRelationship");

app.MapPatch("/digitaltwins/{id}/relationships/{relationshipId}", async (string id, string relationshipId, JsonPatch patch, HttpContext httpContext, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    string? etag = null;
    if (httpContext.Request.Headers.TryGetValue("If-Match", out StringValues etagValues) && etagValues.Count > 0)
    {
        etag = etagValues[0];
    }
    await client.UpdateRelationshipAsync(id, relationshipId, patch, etag, cancellationToken);
    return Results.NoContent();
})
.WithName("UpdateRelationship");

app.MapDelete("/digitaltwins/{id}/relationships/{relationshipId}", async (string id, string relationshipId, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    await client.DeleteRelationshipAsync(id, relationshipId, cancellationToken);
    return Results.NoContent();
})
.WithName("DeleteRelationship");

app.MapPost("/query", async (JsonElement requestBody, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    if (!requestBody.TryGetProperty("query", out JsonElement queryElement) || queryElement.ValueKind != JsonValueKind.String)
    {
        return Results.BadRequest(new { error = "Invalid request body. Expected a JSON object with a 'query' property." });
    }
    string query = queryElement.GetString()!;
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

app.MapDelete("/models/{id}", async (string id, [FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
{
    await client.DeleteModelAsync(id, cancellationToken);
    return Results.NoContent();
})
.WithName("DeleteModel");

// This endpoint is only used for cleanup in tests
// When the client is initiated, a new graph will automatically be created if the specified graph doesn't exist
// Creating and dropping graphs should be done in the control plane
if (app.Environment.IsDevelopment())
{
    app.MapPut("graph/create", ([FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
    {
        return client.CreateGraphAsync(cancellationToken);
    });
    app.MapDelete("graph/delete", ([FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
    {
        return client.DropGraphAsync(cancellationToken);
    });
}

app.MapDefaultEndpoints();
//  app.UseHsts();

app.Run();