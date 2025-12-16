using AgeDigitalTwins;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using AgeDigitalTwins.MCPServerSSE.Configuration;
using AgeDigitalTwins.MCPServerSSE.Endpoints;
using AgeDigitalTwins.MCPServerSSE.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.Age;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<OAuthMetadataOptions>(builder.Configuration.GetSection("MCP"));
builder.Services.Configure<AuthorizationOptions>(builder.Configuration.GetSection("Authorization"));

// Add Npgsql multihost data source with custom settings.
builder.AddNpgsqlMultihostDataSource(
    "agedb",
    configureSettings: settings =>
    {
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
    NpgsqlMultiHostDataSource dataSource = sp.GetRequiredKeyedService<NpgsqlMultiHostDataSource>(
        "agedb"
    );
    string graphName =
        builder.Configuration.GetSection("Parameters")["AgeGraphName"]
        ?? builder.Configuration["Parameters:AgeGraphName"]
        ?? builder.Configuration["AgeGraphName"]
        ?? "digitaltwins";
    Console.WriteLine($"Using graph: {graphName}");
    return new AgeDigitalTwinsClient(dataSource, graphName);
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

builder.Services.AddMcpServer().WithPromptsFromAssembly().WithToolsFromAssembly().WithHttpTransport();

var app = builder.Build();

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
