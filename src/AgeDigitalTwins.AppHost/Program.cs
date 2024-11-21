var builder = DistributedApplication.CreateBuilder(args);

var apiservice = builder.AddProject<Projects.AgeDigitalTwins_ApiService>("apiservice");

builder.Build().Run();
