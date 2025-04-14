using System.Text.Json;
using AgeDigitalTwins;
using AgeDigitalTwins.ApiService;
using AgeDigitalTwins.Models;
using Json.Patch;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.Age;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Npgsql multihost data source with custom settings.
builder.AddNpgsqlMultihostDataSource(
    "agedb",
    configureSettings: settings =>
    {
        settings.ConnectionString ??=
            builder.Configuration.GetConnectionString("agedb")
            ?? builder.Configuration["ConnectionStrings:agedb"]
            ?? builder.Configuration["AgeConnectionString"]
            ?? throw new InvalidOperationException("Connection string is required.");
        // Setting the search path allows to avoid setting this on every connection
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

// Add AgeDigitalTwinsClient
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    NpgsqlMultiHostDataSource dataSource = sp.GetRequiredService<NpgsqlMultiHostDataSource>();
    string graphName =
        builder.Configuration.GetSection("Parameters")["AgeGraphName"]
        ?? builder.Configuration["Parameters:AgeGraphName"]
        ?? builder.Configuration["AgeGraphName"]
        ?? "digitaltwins";
    logger.LogInformation("Using graph: {GraphName}", graphName);
    return new AgeDigitalTwinsClient(dataSource, graphName);
});

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add authentication only if the environment variable is set
var enableAuthentication = builder.Configuration.GetValue<bool>("Authentication:Enabled");

if (enableAuthentication)
{
    builder
        .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(jwtOptions =>
        {
            string? metadataAddress = builder.Configuration["Authentication:MetadataAddress"];
            if (!string.IsNullOrEmpty(metadataAddress))
            {
                jwtOptions.MetadataAddress = metadataAddress;
            }
            jwtOptions.Authority = builder.Configuration["Authentication:Authority"];
            jwtOptions.Audience = builder.Configuration["Authentication:Audience"];
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Authentication:Issuer"],
            };

            jwtOptions.MapInboundClaims = false;
        });

    builder
        .Services.AddAuthorizationBuilder()
        .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
}
else
{
    builder
        .Services.AddAuthorizationBuilder()
        .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());
}

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();
app.MapGet(
        "/digitaltwins/{id}",
        [Authorize]
        (
            string id,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            return client.GetDigitalTwinAsync<JsonDocument>(id, cancellationToken);
        }
    )
    .WithName("GetDigitalTwin");

app.MapPut(
        "/digitaltwins/{id}",
        [Authorize]
        (
            string id,
            JsonDocument digitalTwin,
            HttpContext httpContext,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            string? etag = null;
            if (
                httpContext.Request.Headers.TryGetValue(
                    "If-None-Match",
                    out StringValues etagValues
                )
                && etagValues.Count > 0
            )
            {
                etag = etagValues[0];
            }
            return client.CreateOrReplaceDigitalTwinAsync(id, digitalTwin, etag, cancellationToken);
        }
    )
    .WithName("CreateOrReplaceDigitalTwin");

app.MapPatch(
        "digitaltwins/{id}",
        [Authorize]
        async (
            string id,
            JsonPatch patch,
            HttpContext httpContext,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            string? etag = null;
            if (
                httpContext.Request.Headers.TryGetValue("If-Match", out StringValues etagValues)
                && etagValues.Count > 0
            )
            {
                etag = etagValues[0];
            }
            await client.UpdateDigitalTwinAsync(id, patch, etag, cancellationToken);
            return Results.NoContent();
        }
    )
    .WithName("UpdateDigitalTwin");

app.MapDelete(
        "/digitaltwins/{id}",
        [Authorize]
        async (
            string id,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            await client.DeleteDigitalTwinAsync(id, cancellationToken);
            return Results.NoContent();
        }
    )
    .WithName("DeleteDigitalTwin");

app.MapGet(
        "/digitaltwins/{id}/incomingrelationships",
        [Authorize]
        async (
            string id,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            return Results.Json(
                new
                {
                    value = await client
                        .GetIncomingRelationshipsAsync<JsonDocument>(id, cancellationToken)
                        .ToListAsync(cancellationToken),
                }
            );
        }
    )
    .WithName("ListIncomingRelationships");

app.MapGet(
        "/digitaltwins/{id}/relationships",
        [Authorize]
        async (
            string id,
            HttpContext httpContext,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            string? relationshipName = httpContext.Request.Query["relationshipName"];
            return Results.Json(
                new
                {
                    value = await client
                        .GetRelationshipsAsync<JsonDocument>(
                            id,
                            relationshipName,
                            cancellationToken
                        )
                        .ToListAsync(cancellationToken),
                }
            );
        }
    )
    .WithName("ListRelationships");

app.MapGet(
        "/digitaltwins/{id}/relationships/{relationshipId}",
        [Authorize]
        (
            string id,
            string relationshipId,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            return client.GetRelationshipAsync<JsonDocument>(id, relationshipId, cancellationToken);
        }
    )
    .WithName("GetRelationship");

app.MapPut(
        "/digitaltwins/{id}/relationships/{relationshipId}",
        [Authorize]
        (
            string id,
            string relationshipId,
            JsonDocument relationship,
            HttpContext httpContext,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            string? etag = null;
            if (
                httpContext.Request.Headers.TryGetValue(
                    "If-None-Match",
                    out StringValues etagValues
                )
                && etagValues.Count > 0
            )
            {
                etag = etagValues[0];
            }
            return client.CreateOrReplaceRelationshipAsync(
                id,
                relationshipId,
                relationship,
                etag,
                cancellationToken
            );
        }
    )
    .WithName("CreateOrReplaceRelationship");

app.MapPatch(
        "/digitaltwins/{id}/relationships/{relationshipId}",
        [Authorize]
        async (
            string id,
            string relationshipId,
            JsonPatch patch,
            HttpContext httpContext,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            string? etag = null;
            if (
                httpContext.Request.Headers.TryGetValue("If-Match", out StringValues etagValues)
                && etagValues.Count > 0
            )
            {
                etag = etagValues[0];
            }
            await client.UpdateRelationshipAsync(
                id,
                relationshipId,
                patch,
                etag,
                cancellationToken
            );
            return Results.NoContent();
        }
    )
    .WithName("UpdateRelationship");

app.MapDelete(
        "/digitaltwins/{id}/relationships/{relationshipId}",
        [Authorize]
        async (
            string id,
            string relationshipId,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            await client.DeleteRelationshipAsync(id, relationshipId, cancellationToken);
            return Results.NoContent();
        }
    )
    .WithName("DeleteRelationship");

app.MapPost(
        "/query",
        [Authorize]
        async (
            JsonElement requestBody,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            if (
                !requestBody.TryGetProperty("query", out JsonElement queryElement)
                || queryElement.ValueKind != JsonValueKind.String
            )
            {
                return Results.BadRequest(
                    new
                    {
                        error = "Invalid request body. Expected a JSON object with a 'query' property.",
                    }
                );
            }
            string query = queryElement.GetString()!;
            return Results.Json(
                new
                {
                    value = await client
                        .QueryAsync<JsonDocument>(query, cancellationToken)
                        .ToListAsync(cancellationToken),
                }
            );
        }
    )
    .WithName("Query");

app.MapGet(
        "/models",
        [Authorize]
        async (
            HttpContext httpContext,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            var query = httpContext.Request.Query;

            // Parse query parameters
            string[] dependenciesFor =
            [
                .. query["dependenciesFor"].Where<string>(x => !string.IsNullOrEmpty(x)),
            ];
            bool includeModelDefinition =
                query.ContainsKey("includeModelDefinition")
                && bool.TryParse(query["includeModelDefinition"], out var include)
                && include;

            var options = new GetModelsOptions
            {
                DependenciesFor = dependenciesFor.Length > 0 ? dependenciesFor : null,
                IncludeModelDefinition = includeModelDefinition,
            };

            return Results.Json(
                new
                {
                    value = await client
                        .GetModelsAsync(options, cancellationToken)
                        .ToListAsync(cancellationToken),
                }
            );
        }
    )
    .WithName("ListModels")
    .WithDescription("Retrieves model metadata and, optionally, model definitions");

app.MapPost(
        "/models",
        [Authorize]
        (
            JsonElement[] models,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            return client.CreateModelsAsync(models.Select(m => m.GetRawText()), cancellationToken);
        }
    )
    .WithName("CreateModels");

app.MapDelete(
        "/models/{id}",
        [Authorize]
        async (
            string id,
            [FromServices] AgeDigitalTwinsClient client,
            CancellationToken cancellationToken
        ) =>
        {
            await client.DeleteModelAsync(id, cancellationToken);
            return Results.NoContent();
        }
    )
    .WithName("DeleteModel");

// This endpoint is only used for cleanup in tests
// When the client is initiated, a new graph will automatically be created if the specified graph doesn't exist
// Creating and dropping graphs should be done in the control plane
if (app.Environment.IsDevelopment())
{
    app.MapPut(
        "graph/create",
        ([FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
        {
            return client.CreateGraphAsync(cancellationToken);
        }
    );
    app.MapDelete(
        "graph/delete",
        ([FromServices] AgeDigitalTwinsClient client, CancellationToken cancellationToken) =>
        {
            return client.DropGraphAsync(cancellationToken);
        }
    );
}

app.UseRequestTimeouts();
app.UseOutputCache();

app.MapDefaultEndpoints();

//  app.UseHsts();

app.Run();
