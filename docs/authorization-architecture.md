# Authorization Architecture - Unified Approach

## Overview

Both the **API Service** and **MCP Server** now use a consistent, policy-based authorization system with the shared infrastructure from `ServiceDefaults`. This document explains how authorization works across both applications.

## Core Authorization Flow

```
Request
  ↓
1. JWT Authentication (ASP.NET Core)
   - Validates JWT Bearer token from Auth0
   - Populates HttpContext.User with claims
  ↓
2. MCP Scope Middleware (MCP Server only)
   - Validates OAuth scopes (e.g., "mcp:tools")
   - MCP-specific requirement from RFC 9728
  ↓
3. Policy-Based Authorization (Both)
   - Uses .RequirePermission(ResourceType, PermissionAction)
   - PermissionAuthorizationHandler checks permissions
   - Calls IPermissionProvider.GetPermissionsAsync()
  ↓
4. Endpoint Handler / MCP Tool
```

## Key Differences from Previous Implementation

### ❌ Before (Inconsistent)

**API Service:**

- ✅ Used policy-based authorization with `.RequirePermission()`
- ✅ Used `PermissionAuthorizationHandler`
- ✅ Clean separation of concerns

**MCP Server:**

- ❌ Used custom middleware to manually check permissions
- ❌ Duplicated permission checking logic
- ❌ Mixed middleware and policy approaches
- ✅ Checked OAuth scopes (MCP-specific)

### ✅ After (Unified)

**Both Applications:**

- ✅ Use policy-based authorization with `.RequirePermission()`
- ✅ Use shared `PermissionAuthorizationHandler` from ServiceDefaults
- ✅ Use shared permission models (Permission, ResourceType, PermissionAction)
- ✅ Consistent error handling and logging

**MCP Server (Additional):**

- ✅ Middleware only checks OAuth scopes (MCP-specific requirement)
- ✅ Permission checking delegated to policy system

## Architecture Components

### 1. Shared Infrastructure (ServiceDefaults)

```
ServiceDefaults/
├── Authorization/
│   ├── Models/
│   │   ├── Permission.cs              # Core permission model
│   │   ├── PermissionAction.cs        # Enum: Read, Write, Delete, etc.
│   │   ├── ResourceType.cs            # Enum: DigitalTwins, Mcp, etc.
│   │   └── PermissionParser.cs        # Parses "resource/action" strings
│   ├── IPermissionProvider.cs         # Interface for permission strategies
│   ├── ClaimsPermissionProvider.cs    # Extracts from JWT claims
│   ├── ApiPermissionProvider.cs       # Calls Control Plane API
│   ├── CompositePermissionProvider.cs # Combines multiple providers
│   ├── PermissionAuthorizationHandler.cs  # ASP.NET Core authorization handler
│   ├── PermissionRequirement.cs       # Authorization requirement
│   └── PermissionExtensions.cs        # .RequirePermission() extension
```

### 2. API Service Usage

```csharp
// In endpoint definitions
digitalTwinsGroup
    .MapGet("/{id}", (string id, AgeDigitalTwinsClient client) =>
    {
        return client.GetDigitalTwinAsync(id);
    })
    .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)
    .WithName("GetDigitalTwin");

digitalTwinsGroup
    .MapDelete("/{id}", async (string id, AgeDigitalTwinsClient client) =>
    {
        await client.DeleteDigitalTwinAsync(id);
        return Results.NoContent();
    })
    .RequirePermission(ResourceType.DigitalTwins, PermissionAction.Delete)
    .WithName("DeleteDigitalTwin");
```

### 3. MCP Server Usage

```csharp
// In Program.cs
if (enableAuthentication)
{
    if (enableAuthorization)
    {
        // Use policy-based authorization - same pattern as API Service
        app.MapMcp().RequirePermission(ResourceType.Mcp, PermissionAction.Wildcard);
    }
    else
    {
        app.MapMcp().RequireAuthorization();
    }
}

// Middleware only validates OAuth scopes (MCP-specific)
if (enableAuthorization)
{
    app.UseMiddleware<McpAuthorizationMiddleware>();
}
```

## Why OAuth Scopes AND Permissions?

The MCP Server uses **both** OAuth scopes and permissions:

### OAuth Scopes (RFC 9728 Requirement)

- **Purpose:** Coarse-grained authorization at the OAuth level
- **Example:** `mcp:tools` scope grants access to MCP tool functionality
- **Checked by:** `McpAuthorizationMiddleware`
- **Standard:** Required by MCP OAuth 2.1 specification

### Permissions (Application-Level)

- **Purpose:** Fine-grained authorization for specific resources
- **Example:** `mcp/*` permission grants access to all MCP tools
- **Checked by:** `PermissionAuthorizationHandler` (policy-based)
- **Standard:** Application-specific authorization

**Why Both?**

- OAuth scopes are part of the OAuth 2.1 standard and control access at the protocol level
- Permissions are application-specific and control access at the business logic level
- MCP specification requires OAuth scope validation for compliance
- Permissions allow for more granular control (future: per-tool permissions)

**Analogy:**

- **OAuth Scope:** Like having a security badge to enter the building
- **Permission:** Like having a key to a specific room

## Permission Mapping

### API Service

| Endpoint                                                 | Resource Type | Permission Action | Permission String                 |
| -------------------------------------------------------- | ------------- | ----------------- | --------------------------------- |
| GET /digitaltwins/{id}                                   | DigitalTwins  | Read              | digitaltwins/read                 |
| PUT /digitaltwins/{id}                                   | DigitalTwins  | Write             | digitaltwins/write                |
| PATCH /digitaltwins/{id}                                 | DigitalTwins  | Write             | digitaltwins/write                |
| DELETE /digitaltwins/{id}                                | DigitalTwins  | Delete            | digitaltwins/delete               |
| POST /query                                              | Query         | Read              | query/read                        |
| GET /digitaltwins/{id}/relationships/{relationshipId}    | Relationships | Read              | digitaltwins/relationships/read   |
| PUT /digitaltwins/{id}/relationships/{relationshipId}    | Relationships | Write             | digitaltwins/relationships/write  |
| DELETE /digitaltwins/{id}/relationships/{relationshipId} | Relationships | Delete            | digitaltwins/relationships/delete |
| GET /models                                              | Models        | Read              | models/read                       |
| POST /models                                             | Models        | Write             | models/write                      |
| DELETE /models/{id}                                      | Models        | Delete            | models/delete                     |

### MCP Server

| Endpoint/Tool | Resource Type | Permission Action | Permission String |
| ------------- | ------------- | ----------------- | ----------------- |
| All MCP Tools | Mcp           | Wildcard          | mcp/\*            |

**Future Enhancement:** Per-tool permissions could use:

- `mcp/get_digital_twin` → `Mcp` + `Read`
- `mcp/create_digital_twin` → `Mcp` + `Write`
- `mcp/delete_digital_twin` → `Mcp` + `Delete`

## Permission Providers

Both applications support multiple permission provider strategies:

### 1. ClaimsPermissionProvider (Default)

Extracts permissions from JWT claims:

```json
{
  "sub": "user-123",
  "permissions": ["digitaltwins/read", "digitaltwins/write", "mcp/*"]
}
```

**Configuration:**

```json
{
  "Authorization": {
    "Provider": "Claims",
    "PermissionsClaimName": "permissions"
  }
}
```

### 2. ApiPermissionProvider

Calls Control Plane API to fetch permissions:

```json
{
  "Authorization": {
    "Provider": "Api",
    "ApiProvider": {
      "BaseUrl": "https://ktrlplane.konnektr.io",
      "CheckEndpoint": "/api/v1/permissions/check",
      "ResourceName": "graph-instance-123",
      "CacheExpirationMinutes": 5
    }
  }
}
```

### 3. CompositePermissionProvider

Combines multiple providers (e.g., Claims + API):

- First checks claims for fast validation
- Falls back to API for dynamic permissions
- Caches results for performance

## Error Responses

### 401 Unauthorized (No Authentication)

```json
{
  "error": "unauthorized",
  "error_description": "Authentication required. Please provide a valid Bearer token.",
  "resource_metadata": "https://your-server/.well-known/oauth-protected-resource"
}
```

**HTTP Headers:**

```
WWW-Authenticate: Bearer realm="mcp", resource_metadata="https://..."
```

### 403 Forbidden (Missing Scope)

```json
{
  "error": "insufficient_scope",
  "error_description": "Required scopes: mcp:tools",
  "scope": "mcp:tools"
}
```

### 403 Forbidden (Missing Permission)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "User lacks required permission: mcp/*"
}
```

## Configuration Examples

### API Service (appsettings.json)

```json
{
  "Authentication": {
    "Enabled": true,
    "Authority": "https://auth.konnektr.io",
    "Audience": "https://graph.konnektr.io",
    "Issuer": "https://auth.konnektr.io/"
  },
  "Authorization": {
    "Enabled": true,
    "Provider": "Claims",
    "PermissionsClaimName": "permissions",
    "StrictMode": true
  }
}
```

### MCP Server (appsettings.json)

```json
{
  "Authentication": {
    "Enabled": true,
    "Authority": "https://auth.konnektr.io",
    "Audience": "https://graph.konnektr.io",
    "Issuer": "https://auth.konnektr.io/"
  },
  "Authorization": {
    "Enabled": true,
    "Provider": "Claims",
    "PermissionsClaimName": "permissions",
    "RequiredScopes": ["mcp:tools"],
    "ScopesSupported": ["mcp:tools", "mcp:resources"]
  },
  "MCP": {
    "ServerName": "Konnektr Graph MCP Server",
    "Version": "1.0.0",
    "ResourceServerUrl": "https://your-mcp-server.com"
  }
}
```

## Testing Authorization

### Unit Tests

```csharp
[Fact]
public async Task Endpoint_WithoutPermission_Returns403()
{
    // Arrange
    var token = await GetJwtTokenAsync(permissions: []); // No permissions
    var client = CreateClientWithToken(token);

    // Act
    var response = await client.GetAsync("/digitaltwins/twin-1");

    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task Endpoint_WithPermission_Returns200()
{
    // Arrange
    var token = await GetJwtTokenAsync(permissions: ["digitaltwins/read"]);
    var client = CreateClientWithToken(token);

    // Act
    var response = await client.GetAsync("/digitaltwins/twin-1");

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

### Integration Tests

```csharp
[Fact]
public async Task MCP_Tool_WithoutScope_Returns403()
{
    // Arrange
    var token = await GetJwtTokenAsync(scopes: []); // No scopes
    var client = CreateMcpClient(token);

    // Act
    var response = await client.CallToolAsync("get_digital_twin", new { id = "twin-1" });

    // Assert
    Assert.Equal(403, response.StatusCode);
    Assert.Contains("insufficient_scope", response.Error);
}

[Fact]
public async Task MCP_Tool_WithoutPermission_Returns403()
{
    // Arrange
    var token = await GetJwtTokenAsync(
        scopes: ["mcp:tools"], // Has scope
        permissions: [] // No permissions
    );
    var client = CreateMcpClient(token);

    // Act
    var response = await client.CallToolAsync("get_digital_twin", new { id = "twin-1" });

    // Assert
    Assert.Equal(403, response.StatusCode);
}

[Fact]
public async Task MCP_Tool_WithScopeAndPermission_Succeeds()
{
    // Arrange
    var token = await GetJwtTokenAsync(
        scopes: ["mcp:tools"],
        permissions: ["mcp/*"]
    );
    var client = CreateMcpClient(token);

    // Act
    var response = await client.CallToolAsync("get_digital_twin", new { id = "twin-1" });

    // Assert
    Assert.True(response.IsSuccess);
}
```

## Middleware Order (Critical!)

The order of middleware in `Program.cs` is critical:

```csharp
app.UseAuthentication();              // 1. Validates JWT, populates User
app.UseMiddleware<McpAuthorizationMiddleware>(); // 2. (MCP only) Checks OAuth scopes
app.UseAuthorization();               // 3. Checks authorization policies
app.MapMcp().RequirePermission(...);  // 4. Endpoint with permission policy
```

**Why this order?**

1. **Authentication** must come first to populate `HttpContext.User`
2. **Scope middleware** (MCP only) validates OAuth-level authorization
3. **Authorization** middleware evaluates policies and runs `PermissionAuthorizationHandler`
4. **Endpoint** is only reached if all checks pass

## Benefits of Unified Approach

### Consistency

- ✅ Same permission model across API Service and MCP Server
- ✅ Same authorization handler logic
- ✅ Same error responses and logging

### Maintainability

- ✅ Single source of truth for authorization logic
- ✅ Changes to permission system automatically apply to both
- ✅ Easier to test and debug

### Extensibility

- ✅ Easy to add new permission providers
- ✅ Easy to add new resource types
- ✅ Policy-based system supports complex requirements

### Performance

- ✅ Permission caching built-in
- ✅ No redundant permission checks
- ✅ Efficient policy evaluation

## Migration from Old Approach

If you have existing code using the old middleware approach:

### Before

```csharp
app.UseMiddleware<McpAuthorizationMiddleware>(); // Checked scopes AND permissions
app.MapMcp().RequireAuthorization(); // Generic policy
```

### After

```csharp
app.UseMiddleware<McpAuthorizationMiddleware>(); // Only checks scopes
app.MapMcp().RequirePermission(ResourceType.Mcp, PermissionAction.Wildcard); // Specific permission
```

## Future Enhancements

1. **Per-Tool Permissions** - Fine-grained control over individual MCP tools
2. **Dynamic Permissions** - Load tool permissions from configuration
3. **Permission Hierarchy** - `mcp/*` grants all tool permissions
4. **Audit Logging** - Track all permission checks and denials
5. **Rate Limiting by Permission** - Different rate limits based on permissions

---

**Last Updated:** 2025-01-27  
**Status:** ✅ Production Ready  
**Architecture:** Unified Policy-Based Authorization
