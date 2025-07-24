using System.Text.Json;
using System.Text.Json.Serialization;
using AgeDigitalTwins;
using AgeDigitalTwins.ApiService;
using AgeDigitalTwins.ApiService.Extensions;
using AgeDigitalTwins.ApiService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.Age;
using Scalar.AspNetCore;

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
    int modelCacheExpiration = builder.Configuration.GetValue<int>(
        "Parameters:ModelCacheExpirationSeconds",
        10
    );
    var options = new AgeDigitalTwinsClientOptions
    {
        GraphName = graphName,
        ModelCacheExpiration = TimeSpan.FromSeconds(modelCacheExpiration),
    };
    var client = new AgeDigitalTwinsClient(dataSource, options);

    return client;
});

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

// Add blob storage service
// Use Azure Blob Storage for production, fallback to default for testing/development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IBlobStorageService, DefaultBlobStorageService>();
}
else
{
    builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
}

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
    builder.Services.AddAuthentication();
    builder
        .Services.AddAuthorizationBuilder()
        .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());
}

// Add job resumption service
builder.Services.AddHostedService<JobResumptionService>();

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

// Configure JSON serialization options to omit null values and use camelCase naming
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Configuration["OpenApi:Enabled"] == "true")
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints for Age Digital Twins API
app.MapDigitalTwinsEndpoints();
app.MapRelationshipsEndpoints();
app.MapQueryEndpoints();
app.MapModelsEndpoints();
app.MapImportJobEndpoints();

// When the client is initiated, a new graph will automatically be created if the specified graph doesn't exist
// Creating and dropping graphs should be done in the control plane
if (app.Environment.IsDevelopment())
{
    app.MapGraphEndpoints();
}

app.UseRequestTimeouts();
app.UseOutputCache();

app.MapDefaultEndpoints();

//  app.UseHsts();

app.Run();
