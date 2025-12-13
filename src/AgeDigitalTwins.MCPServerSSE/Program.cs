using AgeDigitalTwins;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.MCPServerSSE.Configuration;
using AgeDigitalTwins.MCPServerSSE.Endpoints;
using AgeDigitalTwins.MCPServerSSE.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.Age;

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
        // Register permission providers based on configuration
        var authzConfig = builder.Configuration.GetSection("Authorization").Get<AuthorizationOptions>();
        
        // Default to claims provider (uses shared implementation from ServiceDefaults)
        builder.Services.AddScoped<IPermissionProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ClaimsPermissionProvider>>();
            return new ClaimsPermissionProvider(
                authzConfig?.PermissionsClaimName ?? "permissions",
                logger
            );
        });
    }

    builder.Services.AddAuthorization();
}

builder.Services.AddMcpServer().WithToolsFromAssembly().WithHttpTransport();

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
    app.MapMcp().RequireAuthorization();
}
else
{
    app.MapMcp();
}

app.Run();
