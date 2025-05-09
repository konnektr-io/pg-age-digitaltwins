using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins.ApiService.Test;

[Trait("Category", "Integration")]
public class HostAndClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource(DefaultTimeout).Token;
        var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AgeDigitalTwins_AppHost>();
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost
            .BuildAsync()
            .WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Act
        var httpClient = app.CreateHttpClient("apiservice");
        await app
            .ResourceNotifications.WaitForResourceHealthyAsync("apiservice")
            .WaitAsync(DefaultTimeout);
        var response = await httpClient.GetAsync("/health", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /* [Fact]
    public async Task GetWebResourceHealth_ReturnsOkStatusCode()
    {
        // Arrange
        var app = new TestingAspireAppHost();

        await app.StartAsync();
        var httpClient = app.CreateHttpClient("apiservice");

        // Act
        var response = await httpClient.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Act
        var response2 = await httpClient!.DeleteAsync("/graph/delete");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    } */

    /* [Fact]
    public async Task ApiServiceEnvVars_ContainCustomGraphName()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AgeDigitalTwins_AppHost>();
        appHost.Configuration["Parameters:AgeGraphName"] = "temp_graph" + Guid.NewGuid().ToString("N");

        var apiservice = (IResourceWithEnvironment)appHost.Resources
            .Single(static r => r.Name == "apiservice");

        // Act
        var envVars = await apiservice.GetEnvironmentVariableValuesAsync(
            DistributedApplicationOperation.Publish);

        // Assert
        Assert.Contains(envVars, static (kvp) =>
        {
            var (key, value) = kvp;

            return key is "services__apiservice__https__0"
                && value is "{apiservice.bindings.https.url}";
        });
    }
 */
    /* [Fact]
    public async Task UseGraphFromEnvVars_CreatesAndDeletesGraph()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AgeDigitalTwins_AppHost>();
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });
        // To output logs to the xUnit.net ITestOutputHelper, consider adding a package from https://www.nuget.org/packages?q=xunit+logging

        await using var app = await appHost.BuildAsync();
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await app.StartAsync();

        // Act
        var httpClient = app.CreateHttpClient("apiservice");
        await resourceNotificationService.WaitForResourceAsync("apiservice", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(60));

        var envVars = await apiservice.GetEnvironmentVariableValuesAsync(
        DistributedApplicationOperation.Publish);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    } */
}
