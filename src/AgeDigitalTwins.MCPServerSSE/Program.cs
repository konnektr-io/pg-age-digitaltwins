using AgeDigitalTwins;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Protocol;
using Npgsql;
using Npgsql.Age;

var builder = WebApplication.CreateBuilder(args);

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

if (enableAuthentication)
{
    builder
        .Services.AddAuthentication(options =>
        {
            options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        })
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
        })
        .AddMcp();

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(
            "age-dt",
            policy =>
                policy.RequireAuthenticatedUser()
                    .RequireClaim("scope", "age-dt:*")
        );
    });
}

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithToolsFromAssembly()
    .WithHttpTransport();


var app = builder.Build();

if (enableAuthentication)
{
    app.UseAuthentication();
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
