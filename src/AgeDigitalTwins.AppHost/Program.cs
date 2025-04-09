var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithImage("konnektr-io/age")
    .WithImageTag("16-bookworm-3")
    .WithImageRegistry("ghcr.io")
    // Set the name of the default database to auto-create on container startup.
    .WithEnvironment("POSTGRES_DB", "app")
    // Mount the SQL scripts directory into the container so that the init scripts run.
    .WithBindMount("./data", "/docker-entrypoint-initdb.d");

var agedb = postgres.AddDatabase("agedb", "app");

// var agedb = builder.AddConnectionString("agedb");
var ageGraphName = builder.AddParameter("AgeGraphName", args.Length > 0 ? args[0] : "digitaltwins");

builder
    .AddProject<Projects.AgeDigitalTwins_ApiService>("apiservice")
    .WithReference(agedb)
    .WithEnvironment("AgeGraphName", ageGraphName);

builder.AddProject<Projects.AgeDigitalTwins_Events>("events").WithReference(agedb);

// var mcp = builder.AddProject<Projects.MCPServerSSE>("server").WithReference(agedb);
// builder.AddMCPInspector().WithSSE(mcp);

builder.Build().Run();
