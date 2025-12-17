
using AgeDigitalTwins;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using AgeDigitalTwins.MCPServerHttp.Configuration;
using AgeDigitalTwins.MCPServerHttp.Endpoints;
using AgeDigitalTwins.MCPServerHttp.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.Age;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Auth Configuration
builder.Services.Configure<OAuthMetadataOptions>(builder.Configuration.GetSection("MCP"));
builder.Services.Configure<AuthorizationOptions>(builder.Configuration.GetSection("Authorization"));

// Add CORS configuration
builder.Services.AddCors(options =>
{
    var corsSection = builder.Configuration.GetSection("Cors");
    var allowedOrigins = corsSection.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" };
    var allowedMethods = corsSection.GetSection("AllowedMethods").Get<string[]>() ?? new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
    var allowedHeaders = corsSection.GetSection("AllowedHeaders").Get<string[]>() ?? new[] { "*" };
    options.AddPolicy("ConfiguredCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithMethods(allowedMethods)
              .WithHeaders(allowedHeaders)
              .AllowCredentials();
        if (allowedOrigins.Contains("*"))
        {
            policy.SetIsOriginAllowed(_ => true);
        }
    });
});

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

    if (enableAuthorization)
    {
        
        // Configure authorization options
        builder.Services.Configure<AgeDigitalTwins.ServiceDefaults.Configuration.AuthorizationOptions>(
            builder.Configuration.GetSection("Authorization")
        );

        // Register permission providers
        var authorizationConfig = builder
            .Configuration.GetSection("Authorization")
            .Get<AgeDigitalTwins.ServiceDefaults.Configuration.AuthorizationOptions>();

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

        // Add permission-based authorization policies with actual requirements
        builder.Services.AddAuthorization(options => options.AddPermissionPolicies());

        // Register authorization handler (inject provider directly)
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
    }
    else
    {
        // Authorization is disabled - register permissive policies for all permissions
        // This prevents "policy not found" errors while allowing all requests
        builder.Services.AddAuthorization(options => options.AddPermissivePermissionPolicies());
    }
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpServer().WithPromptsFromAssembly().WithToolsFromAssembly().WithHttpTransport();

var app = builder.Build();


// Use CORS before authentication and endpoints
app.UseCors("ConfiguredCors");

// OAuth metadata endpoints (must be before authentication middleware)
app.MapOAuthMetadataEndpoints();

if (enableAuthentication)
{
    app.UseAuthentication();
    
    if (enableAuthorization)
    {
        app.UseMiddleware<McpAuthorizationMiddleware>();
    }
    
    app.UseAuthorization();
}

if (enableAuthentication)
{
    if (enableAuthorization)
    {
        // Use policy-based authorization with specific permission requirement
        // This uses the same pattern as API Service endpoints
        app.MapMcp().RequirePermission(ResourceType.Mcp, PermissionAction.Wildcard);
    }
    else
    {
        // Only require authentication, no permission check
        app.MapMcp().RequireAuthorization();
    }
}
else
{
    app.MapMcp();
}

app.MapDefaultEndpoints();

app.Run();
