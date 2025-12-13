# Authorization Refactoring Summary

## Overview

This document summarizes the OAuth 2.1 authorization refactoring completed for the MCP Server SSE and API Service projects, consolidating shared authorization code into the ServiceDefaults project.

## What Was Changed

### 1. Created Shared Authorization Infrastructure (ServiceDefaults)

Created a new `Authorization` folder structure in `AgeDigitalTwins.ServiceDefaults`:

```
Authorization/
├── Models/
│   ├── Permission.cs              # Core permission model with resource/action
│   ├── PermissionAction.cs        # Enum: Read, Write, Delete, Action, Wildcard
│   ├── ResourceType.cs           # Enum including McpTools for MCP permissions
│   └── PermissionParser.cs       # Parses "resource/action" strings (e.g., "mcp/tools")
├── IPermissionProvider.cs        # Interface for permission strategies
└── ClaimsPermissionProvider.cs   # Extracts permissions from JWT claims
```

**Key Features:**
- `Permission.Grants(Permission other)` - Supports wildcard matching (e.g., "digitaltwins/*" grants "digitaltwins/read")
- `ResourceType.McpTools` - New resource type for MCP tool access
- `PermissionParser` - Handles special case for "mcp/tools" format

### 2. Updated API Service to Use Shared Code

**Removed:**
- `Authorization/Models/Permission.cs`
- `Authorization/Models/PermissionAction.cs`
- `Authorization/Models/ResourceType.cs`
- `Authorization/Models/IPermissionProvider.cs`
- `Authorization/Models/PermissionParser.cs`
- `Authorization/Models/` folder (now empty)

**Updated:**
- All Authorization files now import from `AgeDigitalTwins.ServiceDefaults.Authorization.Models`
- `ClaimsPermissionProvider` now wraps the shared implementation with configuration
- Maintained backward compatibility with existing authorization logic

### 3. Refactored MCP Server SSE

**Removed:**
- `Authorization/` folder entirely (duplicate models)
- `Services/ApiServiceClient.cs` (not needed - using direct database access)
- `Configuration/ApiServiceOptions.cs` (not needed)

**Renamed:**
- `Configuration/McpServerOptions.cs` → `Configuration/OAuthMetadataOptions.cs`
  - Resolved naming conflict with `ModelContextProtocol.Server.McpServerOptions`

**Updated:**
- `Program.cs` - Removed ApiServiceClient references, simplified permission provider registration
- `Endpoints/OAuthMetadataEndpoints.cs` - Uses `OAuthMetadataOptions` instead of `McpServerOptions`
- `Middleware/McpAuthorizationMiddleware.cs` - Now checks "mcp/tools" permission using shared authorization

### 4. Architecture Decision: Direct Database Access

The MCP Server now uses **direct database access** via `AgeDigitalTwinsClient` instead of proxying through the API Service:

**Before (Option B - API Proxy):**
```
MCP Server → ApiServiceClient → API Service → AgeDigitalTwinsClient → PostgreSQL
```

**After (Option A - Direct Access):**
```
MCP Server → AgeDigitalTwinsClient → PostgreSQL
API Service → AgeDigitalTwinsClient → PostgreSQL
```

**Benefits:**
- Simpler architecture
- Reduced latency (no extra HTTP hop)
- Easier to maintain
- API Service is just a thin REST wrapper around the SDK

## What's Working

✅ **Shared Authorization Models** - Both projects use the same Permission/ResourceType/PermissionAction types  
✅ **JWT Authentication** - Both projects validate Auth0 JWT tokens  
✅ **Claims-Based Permissions** - Extracts permissions from "permissions" JWT claim  
✅ **OAuth 2.1 Compliance** - MCP Server implements RFC 9728 (Protected Resource Metadata)  
✅ **Scope Validation** - MCP middleware checks for "mcp:tools" scope  
✅ **No Compile Errors** - Both projects compile successfully  

## What Still Needs to be Done

### 1. Tool-Level Permission Protection

Currently, the middleware checks for "mcp/tools" permission globally, but individual MCP tools don't enforce permissions yet.

**Needed:**
- Attribute or mechanism to mark tools with required permissions (similar to `.RequirePermission()` for API endpoints)
- Update `Tools/DigitalTwinsTools.cs` to check permissions before execution

**Example (desired):**
```csharp
[McpTool("get_digital_twin")]
[RequirePermission(ResourceType.DigitalTwins, PermissionAction.Read)]
public async Task<DigitalTwinResponse> GetDigitalTwin(string id)
{
    // Tool implementation
}
```

### 2. Permission Management in KtrlPlane

The "mcp/tools" permission needs to be:
- Added to the KtrlPlane permission system
- Assignable to users/organizations
- Configurable per Graph instance

### 3. Documentation Updates

Update these docs to reflect the new architecture:
- `docs/concepts/mcp-server.mdx` - Remove ApiServiceClient references
- `README.OAuth.md` - Update architecture diagrams
- API documentation - Document "mcp/tools" permission

### 4. Testing

- Integration tests for MCP tool permission checking
- Test permission grant/deny scenarios
- Test OAuth metadata endpoint responses
- Verify JWT validation with Auth0 tokens

### 5. Configuration Examples

Create sample configuration files showing:
- How to enable/disable authorization
- How to configure permission providers
- How to set up Auth0 integration

## Architecture Patterns

### Permission Checking Pattern (API Service)

API endpoints use the `.RequirePermission()` extension:

```csharp
app.MapPost("/digitaltwins/{id}", async (string id, ...) =>
{
    // Handler logic
})
.RequirePermission(ResourceType.DigitalTwins, PermissionAction.Write);
```

This pattern should be adapted for MCP tools.

### Permission Provider Pattern

Both projects support pluggable permission providers:

1. **ClaimsPermissionProvider** (default) - Reads from JWT claims
2. **ApiPermissionProvider** (future) - Calls KtrlPlane API for permissions
3. **CompositePermissionProvider** (API Service) - Combines multiple providers

### Middleware Flow

```
Request
  ↓
JWT Authentication (ValidateToken)
  ↓
McpAuthorizationMiddleware (Check scopes + "mcp/tools" permission)
  ↓
MCP Endpoint (MapMcp)
  ↓
Tool Execution (TODO: Add per-tool permission checks)
```

## Migration Impact

### For Hosted Deployments (Konnektr Graph)
- No changes needed immediately
- Authorization can remain disabled until KtrlPlane integration is ready

### For Self-Hosted Deployments
- Update appsettings to use new `OAuthMetadataOptions` (renamed from `McpServerOptions`)
- Remove any `ApiService:BaseUrl` configuration (no longer used)
- Existing authentication configuration remains unchanged

## Next Steps

1. **Implement Tool Permission Attributes** - Create `[RequirePermission]` attribute for MCP tools
2. **Wire Up Tool Permission Checks** - Integrate permission validation into tool execution pipeline
3. **Add KtrlPlane Integration** - Enable "mcp/tools" permission in the control plane
4. **Write Tests** - Comprehensive integration tests for authorization flow
5. **Update Documentation** - Reflect new architecture in all docs

## Questions & Decisions

### Q: Should we use per-tool permissions or a single "mcp/tools" permission?

**Decision:** Start with a single "mcp/tools" permission for simplicity. Per-tool permissions (e.g., "mcp/get_digital_twin") can be added later if needed.

### Q: Should we consolidate AuthorizationOptions between API Service and MCP Server?

**Decision:** Keep separate for now. They have different needs:
- API Service: Has `StrictMode` for gradual rollout
- MCP Server: Has `RequiredScopes` and `ScopesSupported` for OAuth 2.1

### Q: Should we move AuthorizationOptions to ServiceDefaults too?

**Decision:** Not yet. Configuration classes are project-specific and may diverge over time.

## Resources

- [RFC 9728: Protected Resource Metadata](https://www.rfc-editor.org/rfc/rfc9728.html)
- [OAuth 2.1 Specification](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-10)
- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [Auth0 Documentation](https://auth0.com/docs)

---

**Last Updated:** 2025-01-27  
**Status:** Refactoring Complete, Tool Protection Pending
