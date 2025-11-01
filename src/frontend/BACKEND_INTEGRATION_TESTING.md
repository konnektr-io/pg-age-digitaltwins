# Backend Integration Testing Guide

This guide provides instructions for testing the frontend application with a real backend (either self-hosted AgeDigitalTwins API or Azure Digital Twins).

## Prerequisites

### Option 1: Self-Hosted Backend (AgeDigitalTwins API)

1. **PostgreSQL with Apache AGE**

   - PostgreSQL 12+ with Apache AGE extension installed
   - Database created and AGE initialized

2. **AgeDigitalTwins API Running**

   ```powershell
   # From the repository root
   cd src/AgeDigitalTwins.ApiService
   dotnet run
   ```

   - API should be accessible at `http://localhost:5000` or `https://localhost:5001`

3. **Authentication Setup (Optional)**
   - For NoAuth: No setup required
   - For MSAL: See [MSAL_SETUP.md](./MSAL_SETUP.md)
   - For Auth0: Requires Auth0 tenant configuration

### Option 2: Azure Digital Twins

1. **Azure Digital Twins Instance**

   - Active Azure subscription
   - Azure Digital Twins instance provisioned
   - Endpoint URL (e.g., `https://myinstance.api.wcus.digitaltwins.azure.net`)

2. **Authentication**
   - Azure AD app registration (see [MSAL_SETUP.md](./MSAL_SETUP.md))
   - Appropriate role assignments (Digital Twins Data Owner/Reader)

## Test Data Setup

### Sample DTDL Models

Create these models in your backend before testing:

```json
// Room Model (dtmi:com:example:Room;1)
{
  "@id": "dtmi:com:example:Room;1",
  "@type": "Interface",
  "@context": "dtmi:dtdl:context;2",
  "displayName": "Room",
  "description": "A room in a building",
  "contents": [
    {
      "@type": "Property",
      "name": "temperature",
      "displayName": "Temperature",
      "schema": "double"
    },
    {
      "@type": "Property",
      "name": "humidity",
      "displayName": "Humidity",
      "schema": "double"
    }
  ]
}

// Building Model (dtmi:com:example:Building;1)
{
  "@id": "dtmi:com:example:Building;1",
  "@type": "Interface",
  "@context": "dtmi:dtdl:context;2",
  "displayName": "Building",
  "description": "A building containing rooms",
  "contents": [
    {
      "@type": "Property",
      "name": "name",
      "displayName": "Building Name",
      "schema": "string"
    },
    {
      "@type": "Relationship",
      "name": "contains",
      "displayName": "Contains",
      "target": "dtmi:com:example:Room;1"
    }
  ]
}
```

### Sample Digital Twins

```json
// Building Twin
{
  "$dtId": "building-1",
  "$metadata": {
    "$model": "dtmi:com:example:Building;1"
  },
  "name": "Headquarters"
}

// Room Twins
{
  "$dtId": "room-101",
  "$metadata": {
    "$model": "dtmi:com:example:Room;1"
  },
  "temperature": 22.5,
  "humidity": 45.0
}

{
  "$dtId": "room-102",
  "$metadata": {
    "$model": "dtmi:com:example:Room;1"
  },
  "temperature": 21.8,
  "humidity": 42.0
}
```

### Sample Relationships

```json
{
  "$relationshipId": "building-1-contains-room-101",
  "$sourceId": "building-1",
  "$targetId": "room-101",
  "$relationshipName": "contains"
}

{
  "$relationshipId": "building-1-contains-room-102",
  "$sourceId": "building-1",
  "$targetId": "room-102",
  "$relationshipName": "contains"
}
```

## Testing Checklist

### 1. Connection Management

- [ ] **NoAuth Connection (Self-Hosted)**

  - Create connection with NoAuth provider
  - Endpoint: `http://localhost:5000` or your API URL
  - Verify connection saves successfully
  - Verify connection appears in selector

- [ ] **MSAL Connection (Azure ADT or Self-Hosted with MSAL)**

  - Create connection with MSAL provider
  - Enter Client ID, Tenant ID, Scopes
  - Test connection with valid credentials
  - Verify browser popup for authentication
  - Verify token acquisition and storage

- [ ] **Auth0 Connection (Konnektr Hosted)**

  - Create connection with Auth0 provider
  - Enter Domain, Client ID, Audience
  - Test connection with valid credentials
  - Verify redirect flow
  - Verify token acquisition

- [ ] **Connection Switching**
  - Create multiple connections
  - Switch between connections
  - Verify data refreshes after switch

### 2. Model Management

- [ ] **Load Models**

  - Open ModelSidebar
  - Verify models load from backend
  - Check model display names appear correctly
  - Verify twin counts for each model

- [ ] **Model Search**

  - Enter search query in ModelSidebar
  - Verify filtering works
  - Test partial matches
  - Test case-insensitive search

- [ ] **Model Inspector**

  - Click on a model in sidebar
  - Verify ModelInspector opens
  - Check all model properties display:
    - Model ID
    - Display Name
    - Description
    - Contents (Properties, Telemetry, etc.)
    - Extends (if applicable)

- [ ] **Upload Model**
  - Click "+" button in ModelSidebar
  - Upload a valid DTDL JSON file
  - Verify model appears in sidebar
  - Check for validation errors with invalid DTDL

### 3. Digital Twin Operations

- [ ] **View Twins**

  - Navigate to Twins view
  - Verify twins load from backend
  - Check twin properties display correctly

- [ ] **Create Twin**

  - Click "Create Twin" button
  - Select a model
  - Fill in required properties
  - Submit and verify twin appears

- [ ] **Update Twin**

  - Select an existing twin
  - Edit properties
  - Save changes
  - Verify updated values in inspector

- [ ] **Delete Twin**
  - Select a twin
  - Click delete
  - Confirm deletion
  - Verify twin removed from list

### 4. Relationship Operations

- [ ] **View Relationships**

  - Select a twin with relationships
  - Verify relationships display in graph
  - Check source/target connections

- [ ] **Create Relationship**

  - Select source twin
  - Click "Add Relationship"
  - Select target twin and relationship name
  - Verify relationship appears in graph

- [ ] **Delete Relationship**
  - Select a relationship
  - Delete it
  - Verify removed from graph

### 5. Query Execution

- [ ] **Simple Query**

  - Execute: `SELECT * FROM digitaltwins`
  - Verify results appear
  - Check result formatting

- [ ] **Filtered Query**

  - Execute: `SELECT * FROM digitaltwins WHERE $dtId = 'room-101'`
  - Verify correct twin returned

- [ ] **Model Filter**

  - Execute: `SELECT * FROM digitaltwins WHERE IS_OF_MODEL('dtmi:com:example:Room;1')`
  - Verify only Room twins returned

- [ ] **Join Query**

  - Execute query with JOIN for relationships
  - Verify related twins returned correctly

- [ ] **Query History**

  - Execute multiple queries
  - Verify history saves
  - Click previous query to re-execute

- [ ] **Pagination**
  - Execute query returning many results
  - Verify pagination controls appear
  - Test page navigation

### 6. Graph Visualization

- [ ] **Load Graph**

  - Navigate to Graph view
  - Verify twins render as nodes
  - Verify relationships render as edges

- [ ] **Zoom/Pan**

  - Test zoom in/out
  - Test panning around graph

- [ ] **Node Selection**

  - Click on a node
  - Verify inspector shows twin details

- [ ] **Layout**
  - Test different layout algorithms
  - Verify graph reorganizes

### 7. Error Handling

- [ ] **Invalid Connection**

  - Create connection with wrong endpoint
  - Verify error message displays
  - Check connection marked as failed

- [ ] **Unauthorized Access**

  - Use incorrect credentials
  - Verify 401/403 error handling
  - Check user-friendly error message

- [ ] **Network Errors**

  - Disconnect backend
  - Attempt operations
  - Verify error messages
  - Verify retry/reconnect logic

- [ ] **Invalid Queries**
  - Execute malformed ADT query
  - Verify error message displays
  - Check query editor highlights issues

### 8. Performance Testing

- [ ] **Large Model Count**

  - Load 50+ models
  - Verify sidebar performance
  - Check search responsiveness

- [ ] **Many Twins**

  - Load 1000+ twins
  - Measure query execution time
  - Test pagination performance

- [ ] **Complex Queries**

  - Execute queries with multiple JOINs
  - Monitor execution time
  - Verify results accuracy

- [ ] **Graph Rendering**
  - Display 100+ nodes in graph
  - Test rendering performance
  - Check interaction responsiveness

### 9. Data Consistency

- [ ] **Real-Time Updates**

  - Create twin via API directly
  - Refresh frontend
  - Verify new twin appears

- [ ] **Concurrent Modifications**

  - Open twin in two browser tabs
  - Modify in both
  - Check conflict resolution

- [ ] **ETag Handling**
  - Update twin with outdated ETag
  - Verify 412 Precondition Failed
  - Check refresh logic

## Common Issues & Solutions

### Issue: CORS Errors

**Symptoms**: Browser console shows CORS policy errors

**Solution**:

```csharp
// In AgeDigitalTwins API Program.cs
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Vite dev server
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

### Issue: MSAL Authentication Fails

**Symptoms**: Popup closes immediately or shows error

**Solution**:

1. Verify redirect URI in Azure AD matches: `http://localhost:5173`
2. Check Client ID and Tenant ID are correct
3. Ensure API scopes are granted in Azure AD

### Issue: Models Don't Load

**Symptoms**: Empty sidebar, no models visible

**Solution**:

1. Check browser console for errors
2. Verify backend connection is active
3. Ensure models exist in backend database
4. Check API endpoint returns models: `GET /models`

### Issue: Query Returns No Results

**Symptoms**: Query executes but returns empty array

**Solution**:

1. Verify twins exist in backend
2. Check query syntax is correct
3. Test query directly against API: `POST /query`
4. Review query execution logs

## Testing Scripts

### PowerShell Script: Load Sample Data

```powershell
# load-sample-data.ps1
$endpoint = "http://localhost:5000"

# Upload models
$roomModel = Get-Content ".\room-model.json" | ConvertFrom-Json
Invoke-RestMethod -Uri "$endpoint/models" -Method Post -Body ($roomModel | ConvertTo-Json -Depth 10) -ContentType "application/json"

# Create twins
$building = @{
    '$dtId' = 'building-1'
    '$metadata' = @{ '$model' = 'dtmi:com:example:Building;1' }
    'name' = 'Headquarters'
}
Invoke-RestMethod -Uri "$endpoint/digitaltwins/building-1" -Method Put -Body ($building | ConvertTo-Json) -ContentType "application/json"

Write-Host "Sample data loaded successfully!"
```

## Automated Testing

### Playwright End-to-End Tests (Future)

```typescript
// tests/e2e/connection.spec.ts
import { test, expect } from "@playwright/test";

test("can create NoAuth connection", async ({ page }) => {
  await page.goto("http://localhost:5173");
  await page.click("text=Add Connection");
  await page.fill('[name="name"]', "Test Backend");
  await page.fill('[name="endpoint"]', "http://localhost:5000");
  await page.selectOption('[name="authProvider"]', "NoAuth");
  await page.click("text=Connect");
  await expect(page.locator("text=Test Backend")).toBeVisible();
});
```

## Reporting Issues

When reporting backend integration issues, include:

1. **Environment Details**

   - Backend type (Self-Hosted / Azure ADT)
   - Backend version / Azure region
   - Frontend version/commit
   - Browser and OS

2. **Connection Configuration**

   - Auth provider (NoAuth / MSAL / Auth0)
   - Endpoint URL (redact sensitive info)

3. **Steps to Reproduce**

   - Exact sequence of actions
   - Sample data used
   - Expected vs actual behavior

4. **Logs & Screenshots**
   - Browser console errors
   - Network tab (HAR file)
   - Backend logs (if accessible)
   - Screenshots of error messages

## Next Steps

After completing testing:

1. Document any bugs found in GitHub Issues
2. Update this testing guide with new test cases
3. Create automated tests for critical paths
4. Plan for CI/CD integration testing

---

**Testing Status**: Phase 6.6 - Ready for execution
**Last Updated**: 2025-01-27
