using System.Text.Json;
using AgeDigitalTwins.Api.Controllers;
using AgeDigitalTwins.Api.Models;
using Microsoft.Extensions.Configuration;

namespace AgeDigitalTwins.Api.IntegrationTests.Controllers;

public class DigitalTwinsControllerTests
{
    public DigitalTwinsControllerTests()
    {
        _configuration = new ConfigurationBuilder().AddJsonFile("appsettings.Development.json").Build();
        _controller = new DigitalTwinsController(_configuration);
    }
    private readonly IConfiguration _configuration;
    private readonly DigitalTwinsController _controller;

    [Fact]
    public async void Test1()
    {
        string twinId = "test";
        string sTwin = "{\"_dtId\":\"test\",\"name\":\"test\"}";
        await _controller.PutTwin(twinId, sTwin, CancellationToken.None);
    }
}