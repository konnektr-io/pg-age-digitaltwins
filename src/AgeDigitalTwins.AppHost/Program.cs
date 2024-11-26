var builder = DistributedApplication.CreateBuilder(args);

var ageConnectionString = builder.AddParameter("AgeConnectionString", secret: true);
var ageGraphName = builder.AddParameter("AgeGraphName", () => "digitaltwins", true);
var apiservice = builder.AddProject<Projects.AgeDigitalTwins_ApiService>("apiservice")
    .WithEnvironment("AGE_CONNECTION_STRING", ageConnectionString)
    .WithEnvironment("AGE_GRAPH_NAME", ageGraphName);

builder.Build().Run();
