using AgeDigitalTwins.ApiService.Authorization.Models;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test.Authorization;

/// <summary>
/// Tests for the PermissionParser class.
/// </summary>
public class PermissionParserTests
{
    [Theory]
    [InlineData("digitaltwins/read", ResourceType.DigitalTwins, PermissionAction.Read)]
    [InlineData("digitaltwins/write", ResourceType.DigitalTwins, PermissionAction.Write)]
    [InlineData("digitaltwins/delete", ResourceType.DigitalTwins, PermissionAction.Delete)]
    [InlineData("digitaltwins/*", ResourceType.DigitalTwins, PermissionAction.Wildcard)]
    [InlineData(
        "digitaltwins/relationships/read",
        ResourceType.Relationships,
        PermissionAction.Read
    )]
    [InlineData(
        "digitaltwins/relationships/write",
        ResourceType.Relationships,
        PermissionAction.Write
    )]
    [InlineData(
        "digitaltwins/relationships/delete",
        ResourceType.Relationships,
        PermissionAction.Delete
    )]
    [InlineData("models/read", ResourceType.Models, PermissionAction.Read)]
    [InlineData("models/write", ResourceType.Models, PermissionAction.Write)]
    [InlineData("models/delete", ResourceType.Models, PermissionAction.Delete)]
    [InlineData("query/action", ResourceType.Query, PermissionAction.Action)]
    [InlineData("jobs/imports/read", ResourceType.JobsImports, PermissionAction.Read)]
    [InlineData("jobs/imports/write", ResourceType.JobsImports, PermissionAction.Write)]
    [InlineData("jobs/imports/delete", ResourceType.JobsImports, PermissionAction.Delete)]
    [InlineData("jobs/imports/cancel/action", ResourceType.JobsImports, PermissionAction.Action)]
    [InlineData("digitaltwins/commands/action", ResourceType.DigitalTwins, PermissionAction.Action)]
    public void TryParse_ValidPermissionString_ReturnsTrue(
        string permissionString,
        ResourceType expectedResource,
        PermissionAction expectedAction
    )
    {
        // Act
        var result = PermissionParser.TryParse(permissionString, out var permission);

        // Assert
        Assert.True(result);
        Assert.NotNull(permission);
        Assert.Equal(expectedResource, permission.Resource);
        Assert.Equal(expectedAction, permission.Action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("digitaltwins")]
    [InlineData("digitaltwins/invalid")]
    [InlineData("invalid/read")]
    [InlineData("digitaltwins/read/extra")]
    public void TryParse_InvalidPermissionString_ReturnsFalse(string? permissionString)
    {
        // Act
        var result = PermissionParser.TryParse(permissionString, out var permission);

        // Assert
        Assert.False(result);
        Assert.Null(permission);
    }

    [Fact]
    public void Parse_ValidPermissionString_ReturnsPermission()
    {
        // Arrange
        var permissionString = "digitaltwins/read";

        // Act
        var permission = PermissionParser.Parse(permissionString);

        // Assert
        Assert.NotNull(permission);
        Assert.Equal(ResourceType.DigitalTwins, permission.Resource);
        Assert.Equal(PermissionAction.Read, permission.Action);
    }

    [Fact]
    public void Parse_InvalidPermissionString_ThrowsArgumentException()
    {
        // Arrange
        var permissionString = "invalid";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PermissionParser.Parse(permissionString));
    }

    [Fact]
    public void ParseMany_ValidPermissionStrings_ReturnsPermissions()
    {
        // Arrange
        var permissionStrings = new[]
        {
            "digitaltwins/read",
            "models/write",
            "invalid", // Should be skipped
            "relationships/delete",
        };

        // Act
        var permissions = PermissionParser.ParseMany(permissionStrings).ToList();

        // Assert
        Assert.Equal(2, permissions.Count); // Only 2 valid permissions
        Assert.Contains(
            permissions,
            p => p.Resource == ResourceType.DigitalTwins && p.Action == PermissionAction.Read
        );
        Assert.Contains(
            permissions,
            p => p.Resource == ResourceType.Models && p.Action == PermissionAction.Write
        );
    }

    [Fact]
    public void ParseMany_EmptyCollection_ReturnsEmpty()
    {
        // Arrange
        var permissionStrings = Array.Empty<string>();

        // Act
        var permissions = PermissionParser.ParseMany(permissionStrings).ToList();

        // Assert
        Assert.Empty(permissions);
    }
}
