
using System.Security.Claims;
using AgeDigitalTwins.ServiceDefaults.Authorization;
using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgeDigitalTwins.ApiService.Test.Authorization;

public class CompositePermissionProviderTests
{
    private readonly Mock<ILogger<CompositePermissionProvider>> _loggerMock;

    public CompositePermissionProviderTests()
    {
        _loggerMock = new Mock<ILogger<CompositePermissionProvider>>();
    }

    [Fact]
    public async Task GetPermissionsAsync_CombinesPermissionsFromAllProviders()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var provider1 = new Mock<IPermissionProvider>();
        var provider2 = new Mock<IPermissionProvider>();

        provider1
            .Setup(p => p.GetPermissionsAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    new Permission(ResourceType.DigitalTwins, PermissionAction.Read),
                    new Permission(ResourceType.Models, PermissionAction.Read)
                }
            );

        provider2
            .Setup(p => p.GetPermissionsAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    new Permission(ResourceType.DigitalTwins, PermissionAction.Write),
                    new Permission(ResourceType.Models, PermissionAction.Read) // Duplicate
                }
            );

        var compositeProvider = new CompositePermissionProvider(
            new[] { provider1.Object, provider2.Object },
            _loggerMock.Object
        );

        // Act
        var permissions = await compositeProvider.GetPermissionsAsync(user);

        // Assert
        Assert.Equal(3, permissions.Count);
        Assert.Contains(
            new Permission(ResourceType.DigitalTwins, PermissionAction.Read),
            permissions
        );
        Assert.Contains(
            new Permission(ResourceType.DigitalTwins, PermissionAction.Write),
            permissions
        );
        Assert.Contains(new Permission(ResourceType.Models, PermissionAction.Read), permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_HandlesProviderErrorsGracefully()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var provider1 = new Mock<IPermissionProvider>();
        var provider2 = new Mock<IPermissionProvider>();

        provider1
            .Setup(p => p.GetPermissionsAsync(user, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        provider2
            .Setup(p => p.GetPermissionsAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[] { new Permission(ResourceType.DigitalTwins, PermissionAction.Read) }
            );

        var compositeProvider = new CompositePermissionProvider(
            new[] { provider1.Object, provider2.Object },
            _loggerMock.Object
        );

        // Act
        var permissions = await compositeProvider.GetPermissionsAsync(user);

        // Assert
        Assert.Single(permissions);
        Assert.Contains(
            new Permission(ResourceType.DigitalTwins, PermissionAction.Read),
            permissions
        );
    }
}
