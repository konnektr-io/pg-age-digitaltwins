var builder = DistributedApplication.CreateBuilder(args);

var ageConnectionString = builder.AddParameter("AgeConnectionString", secret: true);
var apiservice = builder.AddProject<Projects.AgeDigitalTwins_ApiService>("apiservice")
    .WithEnvironment("AGE_CONNECTION_STRING", ageConnectionString);

builder.Build().Run();
