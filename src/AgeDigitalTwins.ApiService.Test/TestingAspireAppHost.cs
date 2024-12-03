using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AgeDigitalTwins.ApiService.Test;

public class TestingAspireAppHost : DistributedApplicationFactory
{
    public TestingAspireAppHost() : base(
        typeof(Projects.AgeDigitalTwins_AppHost),
        ["temp_graph_" + Guid.NewGuid().ToString("N")])
    {
    }

    protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });
    }
}