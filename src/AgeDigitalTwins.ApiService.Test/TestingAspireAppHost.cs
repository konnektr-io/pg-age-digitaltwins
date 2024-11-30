using AgeDigitalTwins.Models;
using Aspire.Hosting;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace AgeDigitalTwins.ApiService.Test;

public class TestingAspireAppHost : DistributedApplicationFactory
{
    public TestingAspireAppHost() : base(typeof(Projects.AgeDigitalTwins_AppHost))
    {
    }

    /* protected override void OnBuilderCreating(DistributedApplicationOptions applicationOptions, HostApplicationBuilderSettings hostOptions)
    {
        hostOptions.Configuration!["Parameters:AgeGraphName"] = "temp_graph" + Guid.NewGuid().ToString("N");
    } */

    protected override void OnBuilding(DistributedApplicationBuilder applicationBuilder)
    {
        applicationBuilder.AddParameter("AgeGraphName", "temp_graph" + Guid.NewGuid().ToString("N"));
    }

    protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });
    }
}