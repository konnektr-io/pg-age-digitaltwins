var builder = DistributedApplication.CreateBuilder(args);

var ageConnectionString = builder.AddConnectionString("agedb");
// var ageGraphName = builder.AddParameter("AgeGraphName");

builder.AddProject<Projects.AgeDigitalTwins_ApiService>("apiservice")
    .WithReference(ageConnectionString);
// .WithEnvironment("AgeGraphName", ageGraphName);

builder.Build().Run();
