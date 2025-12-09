using System.Text.Json;
using System.Text.Json.Serialization;
using AgeDigitalTwins;
using AgeDigitalTwins.ApiService;
using AgeDigitalTwins.ApiService.Authorization;
using AgeDigitalTwins.ApiService.Configuration;
using AgeDigitalTwins.ApiService.Extensions;
using AgeDigitalTwins.ApiService.Middleware;
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
            ?? builder.Configuration["Parameters:AgeConnectionString"]
            ?? throw new InvalidOperationException("Connection string is required.");

        // Setting the search path allows to avoid setting this on every connection
        NpgsqlConnectionStringBuilder connectionStringBuilder =
            new(settings.ConnectionString)
            {
                SearchPath = "ag_catalog, \"$user\", public",
                ConnectionIdleLifetime = builder.Configuration.GetValue(
                    "Parameters:ConnectionIdleLifetime",
                    60
                ), // seconds
                ConnectionLifetime = builder.Configuration.GetValue(
                    "Parameters:ConnectionLifetime",
                    300
                ), // seconds
                MaxPoolSize = builder.Configuration.GetValue("Parameters:MaxPoolSize", 100),
                MinPoolSize = builder.Configuration.GetValue("Parameters:MinPoolSize", 0),
                Timeout = builder.Configuration.GetValue("Parameters:ConnectionTimeout", 15), // seconds
                CommandTimeout = builder.Configuration.GetValue("Parameters:CommandTimeout", 30), // seconds
            };
        settings.ConnectionString = connectionStringBuilder.ConnectionString;
    },
    configureDataSourceBuilder: dataSourceBuilder =>
    {
        // UseAge(true) for CNPG images, UseAge() for Apache AGE images
        // Default to CNPG (true) for backward compatibility
        var useCnpgAge = builder.Configuration.GetValue("Parameters:UseCnpgAge", true);
        if (useCnpgAge)
        {
            dataSourceBuilder.UseAge(true);
        }
        else
        {
            dataSourceBuilder.UseAge();
        }
    }
);

// Add AgeDigitalTwinsClient
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    NpgsqlMultiHostDataSource dataSource = sp.GetRequiredService<NpgsqlMultiHostDataSource>();
    string graphName = builder.Configuration["Parameters:AgeGraphName"] ?? "digitaltwins";
    logger.LogInformation("Using graph: {GraphName}", graphName);
    int modelCacheExpiration = builder.Configuration.GetValue(
        "Parameters:ModelCacheExpirationSeconds",
        10
    );
    int defaultBatchSize = builder.Configuration.GetValue("Parameters:DefaultBatchSize", 50);
    int defaultCheckpointInterval = builder.Configuration.GetValue(
        "Parameters:DefaultCheckpointInterval",
        50
    );
    var options = new AgeDigitalTwinsClientOptions
    {
        GraphName = graphName,
        ModelCacheExpiration = TimeSpan.FromSeconds(modelCacheExpiration),
        DefaultBatchSize = defaultBatchSize,
        DefaultCheckpointInterval = defaultCheckpointInterval,
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

var enableAuthorization = builder.Configuration.GetValue<bool>("Authorization:Enabled");

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

    if (enableAuthorization)
    {
        // Configure authorization options
        builder.Services.Configure<AgeDigitalTwins.ApiService.Configuration.AuthorizationOptions>(
            builder.Configuration.GetSection("Authorization")
        );

        // Register permission providers
        var authorizationConfig = builder
            .Configuration.GetSection("Authorization")
            .Get<AgeDigitalTwins.ApiService.Configuration.AuthorizationOptions>();

        // Always register the claims provider
        builder.Services.AddScoped<ClaimsPermissionProvider>();

        // Conditionally register the API provider and its dependencies
        if (authorizationConfig?.Provider?.Equals("Api", StringComparison.OrdinalIgnoreCase) == true)
        {
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient(
                "PermissionsApi",
                client =>
                {
                    if (!string.IsNullOrEmpty(authorizationConfig.ApiProvider?.BaseUrl))
                    {
                        client.BaseAddress = new Uri(authorizationConfig.ApiProvider.BaseUrl);
                    }
                    client.Timeout = TimeSpan.FromSeconds(authorizationConfig.ApiProvider?.TimeoutSeconds ?? 10);
                }
            );
            builder.Services.AddScoped<ApiPermissionProvider>();
        }

        // Register the CompositePermissionProvider as the single IPermissionProvider for the app
        builder.Services.AddScoped<IPermissionProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<CompositePermissionProvider>>();
            var providers = new List<IPermissionProvider>
            {
                sp.GetRequiredService<ClaimsPermissionProvider>()
            };

            if (authorizationConfig?.Provider?.Equals("Api", StringComparison.OrdinalIgnoreCase) == true)
            {
                providers.Add(sp.GetRequiredService<ApiPermissionProvider>());
            }

            return new CompositePermissionProvider(providers, logger);
        });

        // Add permission service (uses the registered provider)
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        
        // Add permission-based authorization policies with actual requirements
        builder.Services.AddAuthorization(options => options.AddPermissionPolicies());

        // Register authorization handler
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
    }
    else
    {
        // Authorization is disabled - register permissive policies for all permissions
        // This prevents "policy not found" errors while allowing all requests
        builder.Services.AddAuthorization(options => options.AddPermissivePermissionPolicies());
    }
}
else
{
    builder.Services.AddAuthentication();
    builder
        .Services.AddAuthorizationBuilder()
        .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());
    
    // Register permissive permission policies to avoid "policy not found" errors
    builder.Services.AddAuthorization(options => options.AddPermissivePermissionPolicies());
}

// Add job resumption service
builder.Services.AddHostedService<JobResumptionService>();

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

// Add rate limiting only if enabled in configuration
var enableRateLimiting = builder.Configuration.GetValue<bool>(
    "Parameters:RateLimitingEnabled",
    true
);

if (enableRateLimiting)
{
    builder.Services.AddRateLimiter(options =>
        options.ConfigureRateLimiting(builder.Configuration)
    );
}

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

// Register weighted query rate limiting middleware and UseRateLimiter only if enabled
if (enableRateLimiting)
{
    app.UseMiddleware<DatabaseProtectionMiddleware>();
    app.UseMiddleware<WeightedQueryRateLimitingMiddleware>();
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints for Age Digital Twins API
app.MapDigitalTwinsEndpoints();
app.MapComponentsEndpoints();
app.MapTelemetryEndpoints();
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
