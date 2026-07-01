var builder = DistributedApplication.CreateBuilder(args);

var ageConnectionString = builder.AddConnectionString("agedb");
var ageGraphName = builder.AddParameter("AgeGraphName", args.Length > 0 ? args[0] : "digitaltwins");
// Allow test hosts to override via environment variables (set by DistributedApplicationFactory.OnBuilderCreated)
var trackLastUpdatedBy = builder.AddParameter("TrackLastUpdatedBy", Environment.GetEnvironmentVariable("Parameters__TrackLastUpdatedBy") ?? "false");
var userIdHeaderName = builder.AddParameter("UserIdHeaderName", Environment.GetEnvironmentVariable("Parameters__UserIdHeaderName") ?? "");
var returnTwinLevelLastUpdatedBy = builder.AddParameter("ReturnTwinLevelLastUpdatedBy", Environment.GetEnvironmentVariable("Parameters__ReturnTwinLevelLastUpdatedBy") ?? "true");

// Check if CNPG_TEST environment variable is set (for testing with CNPG images)
var cnpgTest = Environment.GetEnvironmentVariable("CNPG_TEST");
var useCnpgAge = !string.IsNullOrEmpty(cnpgTest) && cnpgTest.ToLowerInvariant() == "true";

builder
    .AddProject<Projects.AgeDigitalTwins_ApiService>("apiservice")
    .WithReference(ageConnectionString)
    .WithEnvironment("Parameters:AgeGraphName", ageGraphName)
    .WithEnvironment("Parameters:UseCnpgAge", useCnpgAge.ToString())
    .WithEnvironment("Parameters:TrackLastUpdatedBy", trackLastUpdatedBy)
    .WithEnvironment("Parameters:UserIdHeaderName", userIdHeaderName)
    .WithEnvironment("Parameters:ReturnTwinLevelLastUpdatedBy", returnTwinLevelLastUpdatedBy);

// builder.AddProject<Projects.AgeDigitalTwins_Events>("events").WithReference(ageConnectionString);

// var mcp = builder.AddProject<Projects.MCPServerSSE>("server").WithReference(agedb);
// builder.AddMCPInspector().WithSSE(mcp);

builder.Build().Run();
