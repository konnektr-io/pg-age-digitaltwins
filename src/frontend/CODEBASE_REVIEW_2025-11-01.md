# Konnektr Graph Explorer - Comprehensive Codebase Review

**Date**: November 1, 2025  
**Branch**: feat/type-checks  
**Purpose**: Assess readiness for real backend integration and plan authentication abstraction

---

## 🎯 Executive Summary

### Current State

- **UI Framework**: ✅ Fully functional with shadcn/ui components
- **Graph Visualization**: ✅ Sigma.js integrated with real query result transformation
- **State Management**: ✅ Zustand stores properly architected
- **Authentication**: ⚠️ Auth0-specific implementation (needs abstraction)
- **Data Integration**: ⚠️ **Mix of mock and real API calls**

### Critical Findings

1. **Mock Data Usage**: 50% of components still use mock data
2. **Auth Architecture**: Tightly coupled to Auth0, needs abstraction for MSAL/ADT support
3. **API Integration**: Partial - some stores ready, others still mocked
4. **Real Data Testing**: Not yet conducted with actual backend

---

## 📊 Component-by-Component Analysis

### ✅ **Ready for Real Data** (No Mock Dependencies)

#### **Query & Results Components**

- ✅ `QueryExplorer.tsx` - Uses queryStore (which has mocks internally)
- ✅ `QueryResults.tsx` - Accepts any data structure, no mocks
- ✅ `MonacoEditor.tsx` - Pure editor component
- ✅ `GraphViewer.tsx` - Uses transformed data, no direct mocks
- ✅ All table view components (`SimpleTableView`, `GroupedColumnsView`, etc.)

#### **Inspector Components**

- ✅ `Inspector.tsx` - Reads from inspectorStore
- ✅ `TwinInspector.tsx` - Uses `getModelDisplayName` helper (could be abstracted)
- ✅ `RelationshipInspector.tsx` - Pure component
- ⚠️ `ModelInspector.tsx` - **Uses mockModels directly** (line 4)

#### **Layout Components**

- ✅ `GraphHeader.tsx` - Uses ConnectionSelector/Status
- ✅ `MainContent.tsx` - Pure layout
- ✅ `StatusBar.tsx` - Reads from stores
- ⚠️ `ModelSidebar.tsx` - **Uses mockModels and mockDigitalTwins** (lines 26-31, 49)

#### **UI Components**

- ✅ All shadcn/ui components - Pure presentation
- ✅ `ConnectionSelector.tsx` - Uses connectionStore
- ✅ `ConnectionStatus.tsx` - Uses connectionStore
- ✅ `CookieConsent.tsx` - Pure component

---

### ⚠️ **Mixed: Partial Real Data Integration**

#### **Stores (Critical Infrastructure)**

| Store                  | Status   | Mock Usage           | Real API Calls  | Notes                                   |
| ---------------------- | -------- | -------------------- | --------------- | --------------------------------------- |
| `queryStore.ts`        | ⚠️ Mixed | Lines 161-198        | None yet        | **executeQuery uses mock data routing** |
| `digitalTwinsStore.ts` | ✅ Ready | Mock token (line 36) | Yes (Azure SDK) | Needs real Auth integration             |
| `modelsStore.ts`       | ✅ Ready | Mock token (line 26) | Yes (Azure SDK) | Needs real Auth integration             |
| `connectionStore.ts`   | ✅ Ready | Default localhost    | None            | Pure state management                   |
| `inspectorStore.ts`    | ✅ Ready | None                 | None            | Pure state management                   |
| `workspaceStore.ts`    | ✅ Ready | None                 | None            | Pure state management                   |

**Key Finding**: Stores have real Azure SDK integration but use mock tokens. QueryStore is **entirely mocked**.

---

### 🔴 **Blocked: Requires Authentication Refactor**

#### **Services**

- 🔴 `Auth0TokenCredential.ts` - **Auth0-specific implementation**
- ✅ `digitalTwinsClientFactory.ts` - Generic, accepts any TokenCredential

#### **Hooks**

- 🔴 `useDigitalTwinsClient.ts` - **Hardcoded to Auth0**

#### **Providers**

- 🔴 `Auth0ProviderWithConfig.tsx` - **Auth0-specific**

---

## 🔍 Mock Data Usage Breakdown

### Direct Mock Imports (Must Be Replaced)

```typescript
// 1. ModelSidebar.tsx (lines 26-31)
import {
  mockModels as realModels,
  mockDigitalTwins,
  getModelDisplayName,
} from "@/mocks/digitalTwinData";

// 2. ModelInspector.tsx (line 4)
import { mockModels } from "@/mocks/digitalTwinData";

// 3. TwinInspector.tsx (line 7)
import { getModelDisplayName } from "@/mocks/digitalTwinData";

// 4. queryStore.ts (lines 161-198)
const { mockQueryResults, mockDigitalTwins, formatTwinForDisplay } =
  await import("@/mocks/digitalTwinData");
```

### Helper Functions to Extract

- `getModelDisplayName(modelId)` - Should move to `@/utils/dtdlHelpers.ts`
- `formatTwinForDisplay(twin)` - Should move to `@/utils/dtdlHelpers.ts`

---

## 🏗️ Architecture Assessment

### Current Authentication Flow

```
App.tsx (no auth wrapper)
  └─> Component uses useDigitalTwinsClient()
       └─> useAuth0() from @auth0/auth0-react
            └─> Auth0TokenCredential
                 └─> DigitalTwinsClient
```

**Problems**:

1. ❌ Tightly coupled to Auth0
2. ❌ Cannot support MSAL for Azure Digital Twins
3. ❌ No abstraction for self-hosted scenarios
4. ❌ Not configurable at runtime

---

## 🎯 Proposed Authentication Architecture

### Design Goals

1. **Multi-Provider Support**: Auth0, MSAL, Generic OAuth
2. **PKCE Flow**: Standard OAuth 2.0 with PKCE for all providers
3. **Runtime Configuration**: Select provider via environment/config
4. **Backward Compatible**: Existing components work unchanged
5. **Type-Safe**: Proper TypeScript interfaces

### Proposed Structure

```
src/services/auth/
  ├── types.ts                    # Common interfaces
  ├── AuthProvider.tsx            # Generic auth context provider
  ├── TokenCredentialFactory.ts   # Factory for Azure SDK credentials
  ├── providers/
  │   ├── Auth0Provider.tsx       # Auth0 PKCE implementation
  │   ├── MsalProvider.tsx        # MSAL PKCE implementation
  │   └── GenericOAuthProvider.tsx # Generic OAuth PKCE
  └── hooks/
      ├── useAuth.ts              # Generic auth hook
      └── useDigitalTwinsClient.ts # Updated to use generic auth
```

### Provider Interface

```typescript
// src/services/auth/types.ts
export interface AuthConfig {
  provider: "auth0" | "msal" | "oauth";
  domain?: string; // Auth0 domain or OAuth issuer
  clientId: string;
  audience?: string; // Auth0 audience or OAuth scope
  tenantId?: string; // MSAL tenant ID
  authorizeEndpoint?: string; // Generic OAuth
  tokenEndpoint?: string; // Generic OAuth
  scopes?: string[];
}

export interface AuthContextValue {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: User | null;
  loginWithRedirect: () => Promise<void>;
  logout: () => void;
  getAccessToken: (scopes?: string[]) => Promise<string>;
}

export interface TokenCredentialProvider {
  getToken(scopes: string | string[]): Promise<AccessToken | null>;
}
```

### Configuration Examples

```bash
# .env.development (Konnektr hosted - Auth0)
VITE_AUTH_PROVIDER=auth0
VITE_AUTH0_DOMAIN=auth.konnektr.io
VITE_AUTH0_CLIENT_ID=xxx
VITE_AUTH0_AUDIENCE=https://api.graph.konnektr.io

# .env.adt (Azure Digital Twins - MSAL)
VITE_AUTH_PROVIDER=msal
VITE_MSAL_CLIENT_ID=xxx
VITE_MSAL_TENANT_ID=xxx
VITE_MSAL_SCOPES=https://digitaltwins.azure.net/.default

# .env.selfhosted (Self-hosted - Generic OAuth)
VITE_AUTH_PROVIDER=oauth
VITE_OAUTH_ISSUER=https://your-auth.example.com
VITE_OAUTH_CLIENT_ID=xxx
VITE_OAUTH_AUDIENCE=https://api.example.com
VITE_OAUTH_SCOPES=openid,profile,api
```

---

## 📋 Migration Plan: Mock Data to Real Data

### Phase 6.1: Extract Reusable Helpers (Low Risk)

**Priority**: HIGH  
**Effort**: 2-4 hours

**Tasks**:

1. Move `getModelDisplayName()` to `src/utils/dtdlHelpers.ts`
2. Move `formatTwinForDisplay()` to `src/utils/dtdlHelpers.ts`
3. Update imports in `ModelSidebar.tsx`, `TwinInspector.tsx`, `ModelInspector.tsx`

**Files Modified**: 4
**Risk**: Low (pure functions)

---

### Phase 6.2: Implement Authentication Abstraction (Medium Risk)

**Priority**: HIGH  
**Effort**: 1-2 days

**Tasks**:

1. Create `src/services/auth/` directory structure
2. Implement `AuthProvider.tsx` with context
3. Implement `Auth0Provider.tsx` (migrate existing logic)
4. Implement `MsalProvider.tsx` (new, for ADT)
5. Implement `GenericOAuthProvider.tsx` (new, for self-hosted)
6. Create `TokenCredentialFactory.ts`
7. Update `useDigitalTwinsClient` hook to use generic auth
8. Add provider selection logic in `main.tsx`
9. Update environment configuration

**Files Created**: 7
**Files Modified**: 3 (main.tsx, useDigitalTwinsClient.ts, App.tsx)
**Risk**: Medium (breaking change if not careful)

**Testing Requirements**:

- [ ] Auth0 flow still works (Konnektr)
- [ ] MSAL flow works (ADT)
- [ ] Token refresh works for all providers
- [ ] Logout clears all state
- [ ] Error handling for each provider

---

### Phase 6.3: Replace QueryStore Mock Data (High Risk)

**Priority**: MEDIUM  
**Effort**: 4-6 hours

**Current Issue**: `executeQuery()` in queryStore.ts is entirely mocked (lines 150-210)

**Tasks**:

1. Implement real query execution via Azure SDK:
   ```typescript
   // Use client.queryTwins() for ADT queries
   const client = getClient();
   const queryResult = client.queryTwins(query);
   const results = [];
   for await (const item of queryResult) {
     results.push(item);
   }
   ```
2. Remove mock data routing logic
3. Add proper error handling for query syntax errors
4. Add query validation before execution
5. Update result transformation for real API responses

**Files Modified**: 1 (queryStore.ts)
**Risk**: High (core functionality)

**Testing Requirements**:

- [ ] Test with SELECT queries
- [ ] Test with MATCH queries
- [ ] Test with JOIN queries
- [ ] Test with aggregations (AVG, COUNT, etc.)
- [ ] Test with nested results (COLLECT)
- [ ] Test error handling for invalid queries
- [ ] Test pagination with large result sets

---

### Phase 6.4: Replace ModelSidebar Mock Data (Medium Risk)

**Priority**: MEDIUM  
**Effort**: 2-3 hours

**Tasks**:

1. Update ModelSidebar to fetch models from modelsStore:
   ```typescript
   const { models, fetchModels } = useModelsStore();
   useEffect(() => {
     fetchModels();
   }, []);
   ```
2. Fetch twin counts per model from API
3. Remove direct mock imports
4. Add loading/error states

**Files Modified**: 1 (ModelSidebar.tsx)
**Risk**: Medium (UI component with business logic)

---

### Phase 6.5: Replace ModelInspector Mock Data (Low Risk)

**Priority**: LOW  
**Effort**: 1 hour

**Tasks**:

1. Update to use `modelsStore` instead of `mockModels`
2. Handle loading state while fetching model details

**Files Modified**: 1 (ModelInspector.tsx)
**Risk**: Low (simple component)

---

### Phase 6.6: End-to-End Testing with Real Backend (Critical)

**Priority**: HIGH  
**Effort**: 1-2 days

**Prerequisites**:

- [ ] Backend API running (AgeDigitalTwins.ApiService)
- [ ] Auth0 configured for dev environment
- [ ] Test data loaded in PostgreSQL/AGE

**Test Scenarios**:

1. **Authentication Flow**:

   - [ ] Login with Auth0
   - [ ] Token refresh
   - [ ] Logout
   - [ ] Expired token handling

2. **Connection Management**:

   - [ ] Add new connection
   - [ ] Switch between connections
   - [ ] Invalid connection handling

3. **Models**:

   - [ ] Fetch all models
   - [ ] View model details
   - [ ] Upload new model
   - [ ] Delete model

4. **Digital Twins**:

   - [ ] List all twins
   - [ ] Create new twin
   - [ ] Update twin properties
   - [ ] Delete twin
   - [ ] View twin relationships

5. **Queries**:

   - [ ] Execute SELECT query
   - [ ] Execute MATCH query
   - [ ] Execute JOIN query
   - [ ] View results in table mode
   - [ ] View results in graph mode
   - [ ] Export results to CSV
   - [ ] Query history persistence

6. **Inspector**:
   - [ ] Click twin in table to inspect
   - [ ] Click node in graph to inspect
   - [ ] Edit twin properties
   - [ ] View relationships

---

## 🚀 Recommended Execution Order

### Week 1: Authentication Abstraction

**Day 1-2**: Phase 6.2 (Auth abstraction)

- Critical for all other work
- Enables testing with real backend

**Day 3**: Phase 6.1 (Extract helpers)

- Quick wins
- Reduces mock dependencies

### Week 2: Real Data Integration

**Day 4**: Phase 6.3 (QueryStore)

- High risk, needs careful testing
- Core functionality

**Day 5**: Phase 6.4 & 6.5 (Model components)

- Medium/low risk
- Can be done in parallel

### Week 3: Testing & Polish

**Day 6-10**: Phase 6.6 (E2E testing)

- Comprehensive testing
- Bug fixes and refinements

---

## 📊 Risk Assessment

| Phase           | Risk Level | Impact if Fails                 | Mitigation                    |
| --------------- | ---------- | ------------------------------- | ----------------------------- |
| 6.1 (Helpers)   | 🟢 Low     | Minor - isolated functions      | Unit tests                    |
| 6.2 (Auth)      | 🟡 Medium  | High - affects all API calls    | Feature flag, gradual rollout |
| 6.3 (Query)     | 🔴 High    | High - core feature             | Extensive testing, fallback   |
| 6.4 (Sidebar)   | 🟡 Medium  | Low - UI only                   | Easy to revert                |
| 6.5 (Inspector) | 🟢 Low     | Low - isolated component        | Easy to revert                |
| 6.6 (Testing)   | 🟡 Medium  | Critical - production readiness | Comprehensive test plan       |

---

## 🎯 Success Criteria

### Authentication

- ✅ Support Auth0 (Konnektr hosted)
- ✅ Support MSAL (Azure Digital Twins)
- ✅ Support Generic OAuth (self-hosted)
- ✅ PKCE flow for all providers
- ✅ Token refresh works correctly
- ✅ Runtime provider selection

### Data Integration

- ✅ Zero mock data in production code
- ✅ All stores use real API calls
- ✅ All components fetch real data
- ✅ Proper loading/error states
- ✅ Performance acceptable (<2s for queries)

### Testing

- ✅ Unit tests for new auth code (>80% coverage)
- ✅ Integration tests pass
- ✅ Manual E2E testing complete
- ✅ No console errors in production build
- ✅ All features work with real backend

---

## 📝 Next Steps

1. **Review this document** with team
2. **Approve architecture proposal** for authentication
3. **Set up test backend** with Auth0 + real data
4. **Begin Phase 6.1** (extract helpers) - quick win
5. **Implement Phase 6.2** (auth abstraction) - critical path
6. **Test incrementally** - don't wait for full completion

---

## 📚 Related Documents

- [DEVELOPMENT_PROGRESS.md](./DEVELOPMENT_PROGRESS.md) - Current feature status
- [DEVELOPMENT_PLAN.md](../.github/DEVELOPMENT_PLAN.md) - Original roadmap
- [PLATFORM_SCOPE.md](../.github/PLATFORM_SCOPE.md) - Architecture boundaries

---

**Status**: ⚠️ **Ready for Phase 6 Implementation**  
**Blockers**: None - can proceed with auth abstraction  
**Next Review**: After Phase 6.2 completion
