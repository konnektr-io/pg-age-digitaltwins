using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AgeDigitalTwins.ApiService.Authorization;
using AgeDigitalTwins.ApiService.Authorization.Models;
using AgeDigitalTwins.ApiService.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test.Authorization;

/// <summary>
/// Tests for the ApiPermissionProvider class.
/// </summary>
public class ApiPermissionProviderTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<ApiPermissionProvider>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly AuthorizationOptions _options;

    public ApiPermissionProviderTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<ApiPermissionProvider>>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();

        _options = new AuthorizationOptions
        {
            ApiProvider = new ApiProviderOptions
            {
                BaseUrl = "https://test.api.com",
                CheckEndpoint = "/api/v1/permissions/check",
                ResourceName = "digitaltwins",
                CacheExpirationMinutes = 5,
                TimeoutSeconds = 10,
                TokenEndpoint = "https://test.api.com/oauth/token",
                Audience = "https://test.api.com/api",
                ClientId = "dummy-client-id",
                ClientSecret = "dummy-client-secret"
            },
        };
    }

    private ApiPermissionProvider CreateProvider()
    {
        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri(_options.ApiProvider!.BaseUrl),
        };

        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        return new ApiPermissionProvider(
            _httpClientFactoryMock.Object,
            _cacheMock.Object,
            Options.Create(_options),
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task GetPermissionsAsync_UnauthenticatedUser_ReturnsEmpty()
    {
        // Arrange
        var provider = CreateProvider();
        var user = new ClaimsPrincipal();

        // Act
        var permissions = await provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Empty(permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_CacheHit_ReturnsCachedPermissions()
    {
        // Arrange
        var provider = CreateProvider();
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user123") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        var cachedPermissions = new List<Permission>
        {
            new(ResourceType.DigitalTwins, PermissionAction.Read),
        }.AsReadOnly();

        _cacheMock
            .Setup(c => c.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny))
            .Returns(
                (object key, out object? value) =>
                {
                    value = cachedPermissions;
                    return true;
                }
            );

        // Act
        var permissions = await provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Single(permissions);
        _httpHandlerMock
            .Protected()
            .Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
    }

    [Fact]
    public async Task GetPermissionsAsync_ApiSuccess_ReturnsPermissions()
    {
        // Arrange
        var provider = CreateProvider();
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user123") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        var permissionStrings = new[] { "digitaltwins/read", "models/write" };

        // Mock token endpoint response
        var tokenResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"access_token\":\"dummy-token\",\"expires_in\":3600}")
        };

        // Mock permissions API response
        var permissionsResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(permissionStrings)),
        };

        int callCount = 0;
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                // First call is token endpoint, second is permissions API
                if (callCount == 0)
                {
                    callCount++;
                    return tokenResponse;
                }
                return permissionsResponse;
            });

        object? cacheValue = null;
        _cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue)).Returns(false);
        _cacheMock.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

        // Act
        var permissions = await provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Equal(2, permissions.Count);
    }

    [Fact]
    public async Task GetPermissionsAsync_ApiFailure_ReturnsEmpty()
    {
        // Arrange
        var provider = CreateProvider();
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user123") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError }
            );

        object? cacheValue = null;
        _cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue)).Returns(false);

        // Act
        var permissions = await provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Empty(permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_HttpException_ReturnsEmpty()
    {
        // Arrange
        var provider = CreateProvider();
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user123") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        object? cacheValue = null;
        _cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue)).Returns(false);

        // Act
        var permissions = await provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Empty(permissions);
    }
}
