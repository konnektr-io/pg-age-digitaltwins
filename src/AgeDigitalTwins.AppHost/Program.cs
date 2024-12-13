var builder = DistributedApplication.CreateBuilder(args);

var ageConnectionString = builder.AddConnectionString("agedb");
var ageGraphName = builder.AddParameter("AgeGraphName", args.Length > 0 ? args[0] : "digitaltwins");

builder
    .AddProject<Projects.AgeDigitalTwins_ApiService>("apiservice")
    .WithReference(ageConnectionString)
    .WithEnvironment("AgeGraphName", ageGraphName);

// builder.AddProject<Projects.AgeDigitalTwins_Events>("events").WithReference(ageConnectionString);

builder.Build().Run();
