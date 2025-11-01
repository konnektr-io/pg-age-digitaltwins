# Konnektr Graph Explorer - Development Plan

## Current Status Overview

**Last Updated**: November 1, 2025  
**Status**: 🟡 Phase 5 Complete, Phase 6 In Progress

### ✅ Completed Features (Phase 1-5)

- **Core Layout & Navigation**: Resizable panels, header, sidebar, inspector ✅
- **Monaco Editor**: Cypher syntax highlighting, IntelliSense, autocompletion ✅
- **Query Results**: Table display with pagination, export, 4 view modes ✅
- **Query History**: Searchable history with metadata tracking ✅
- **State Management**: Zustand stores for workspace, connection, and query state ✅
- **API Integration**: Stores have Azure SDK methods (QueryStore still uses mocks) ⚠️
- **Digital Twins Client**: Mock token credential for development ⚠️
- **Sigma.js Graph Viewer**: Interactive graph visualization with real data transformation ✅
- **Inspector System**: Click-to-inspect for twins, relationships, and models ✅
- **Multi-View Results**: Table, Graph, and Raw JSON views ✅
- **Advanced Table Features**: 4 table modes (Simple, Grouped, Flat, Expandable) ✅
- **Cookie Consent**: GTAG + privacy-compliant popup ✅

### 🚨 Current Blockers (Phase 6 Requirements)

**See [CODEBASE_REVIEW_2025-11-01.md](../src/frontend/CODEBASE_REVIEW_2025-11-01.md) for comprehensive analysis**

1. **Authentication Abstraction Needed** 🔴 **BLOCKING**
   - Current: Hardcoded to Auth0
   - Required: Support Auth0, MSAL (Azure ADT), Generic OAuth
   - Impact: Cannot connect to Azure Digital Twins or self-hosted instances
   - **Phase 6.2 addresses this**

2. **QueryStore Uses Mock Data** 🟡 **HIGH PRIORITY**
   - Current: `executeQuery()` returns hardcoded mock data
   - Required: Use Azure SDK `client.queryTwins()`
   - Impact: Queries don't execute against real backend
   - **Phase 6.3 addresses this**

3. **Components Still Using Mocks** 🟡 **MEDIUM PRIORITY**
   - `ModelSidebar.tsx`, `ModelInspector.tsx` use mockModels
   - Impact: UI doesn't reflect real backend state
   - **Phase 6.4-6.5 addresses this**

4. **No Real Backend Testing** 🔴 **CRITICAL**
   - All testing done with mock data
   - Unknown production readiness
   - **Phase 6.6 addresses this**

### ✅ Previously Resolved Issues (Phase 5)

1. **Graph View Data Mismatch** ✅ **FIXED**
   - Created transformation layer (`queryResultsTransformer.ts`)
   - GraphViewer now displays actual query results

2. **Component Duplication** ✅ **RESOLVED**
   - QueryResultsImproved.tsx deleted after feature extraction

3. **Missing Advanced Table Features** ✅ **IMPLEMENTED**
   - Four table view modes integrated into QueryResults.tsx
   - Smart view selection based on data structure

## Phase 5: Component Consolidation & Enhancement ✅ **COMPLETED**

### 5.1 Graph View Data Transformation (HIGH PRIORITY)

**Problem**: Graph view displays mock data instead of query results

**Solution**:
- Create transformation layer to parse query results
- Detect twins and relationships in result data
- Handle various query result schemas
- Graceful fallback for non-graph data

**Implementation**:

```typescript
// src/utils/queryResultsTransformer.ts
interface TransformedResults {
  twins: BasicDigitalTwin[];
  relationships: BasicRelationship[];
  hasGraphData: boolean;
}

export function transformResultsToGraph(
  results: unknown[]
): TransformedResults {
  // Detect and parse twins (objects with $dtId and $metadata)
  // Detect and parse relationships (objects with $relationshipId)
  // Return structured data for GraphViewer
}
```

**Files to Create**:
- `src/utils/queryResultsTransformer.ts` - Data transformation logic
- `src/utils/queryResultsTransformer.test.ts` - Unit tests

**Files to Modify**:
- `src/components/query/QueryResults.tsx` (lines 309-315)
  - Replace `mockDigitalTwins` and `mockRelationships` with transformed data
  - Add conditional rendering based on `hasGraphData`
  - Show helpful message when graph view isn't applicable

**Acceptance Criteria**:
- ✅ Graph view displays actual query results
- ✅ Handles flat and nested result structures
- ✅ Graceful fallback when results don't contain graph data
- ✅ Type-safe transformation with proper error handling

### 5.2 Merge Advanced Table Features (MEDIUM PRIORITY)

**Problem**: Advanced nested data handling exists but isn't integrated

**Solution**:
- Extract table view components from QueryResultsImproved.tsx
- Create modular table view system
- Add view mode selector for different table layouts
- Implement smart view detection based on data structure

**Implementation**:

Create specialized table components:
1. **SimpleTable**: Current flat table view (existing)
2. **GroupedColumnsTable**: Expandable column groups for entities
3. **FlatColumnsTable**: Prefixed column names for nested properties
4. **ExpandableRowsTable**: Master-detail view with row expansion

**Files to Create**:
- `src/components/query/table-views/SimpleTable.tsx` - Extract current logic
- `src/components/query/table-views/GroupedColumnsTable.tsx` - From QueryResultsImproved
- `src/components/query/table-views/FlatColumnsTable.tsx` - From QueryResultsImproved
- `src/components/query/table-views/ExpandableRowsTable.tsx` - From QueryResultsImproved
- `src/utils/dataStructureDetector.ts` - Detect data complexity

**Files to Modify**:
- `src/components/query/QueryResults.tsx`
  - Add table view mode selector (Simple | Grouped | Flat | Expandable)
  - Conditional rendering based on selected mode
  - Auto-detect best view for data structure

**Acceptance Criteria**:
- ✅ All table view modes work seamlessly
- ✅ User can switch between views
- ✅ Smart default view based on data structure
- ✅ Inspector integration works across all views
- ✅ Export works for all view modes

### 5.3 Clean Up Duplicate Components (LOW PRIORITY)

**Problem**: Unused components create maintenance burden

**Solution**:
- Archive or delete unused components
- Document component decisions
- Update imports and references

**Files to Archive/Delete**:
- `src/components/query/QueryExplorerSimple.tsx` - Superseded by Monaco version
- `src/components/query/QueryResultsImproved.tsx` - After feature extraction

**Files to Modify**:
- `.github/DEVELOPMENT_PLAN.md` - Update component documentation
- `src/frontend/DEVELOPMENT_PROGRESS.md` - Reflect current state

**Acceptance Criteria**:
- ✅ No unused components in codebase
- ✅ Clear documentation of component architecture
- ✅ No broken imports or references

### 5.4 Type Safety Enhancement (LOW PRIORITY)

**Problem**: Inconsistent types across query components

**Solution**:
- Create unified type definitions
- Add runtime type guards
- Ensure type safety throughout query pipeline

**Files to Create**:
- `src/types/QueryResults.ts` - Unified result types
  ```typescript
  export type QueryResult = Record<string, unknown>;
  export type QueryResults = QueryResult[];
  
  export interface QueryResultsMetadata {
    columns: string[];
    hasNestedEntities: boolean;
    hasGraphData: boolean;
    dataComplexity: 'simple' | 'nested' | 'complex';
  }
  ```

**Files to Modify**:
- `src/components/query/QueryResults.tsx` - Use unified types
- `src/stores/queryStore.ts` - Update result types
- All table view components - Consistent prop types

**Acceptance Criteria**:
- ✅ No `any` types in query components
- ✅ Runtime type guards for result validation
- ✅ Consistent types across all components

## Phase 6: Real Backend Integration (CURRENT PHASE)

**Status**: 🔄 In Progress  
**Goal**: Replace all mock data with real API calls and abstract authentication layer  
**Timeline**: 2-3 weeks

**See**: [CODEBASE_REVIEW_2025-11-01.md](../src/frontend/CODEBASE_REVIEW_2025-11-01.md) for comprehensive implementation details

### 6.1 Extract Reusable Helpers
**Priority**: HIGH | **Effort**: 2-4 hours | **Risk**: 🟢 Low

**Tasks**:
- [ ] Move `getModelDisplayName()` to `src/utils/dtdlHelpers.ts`
- [ ] Move `formatTwinForDisplay()` to `src/utils/dtdlHelpers.ts`
- [ ] Update imports in `ModelSidebar.tsx`, `TwinInspector.tsx`, `ModelInspector.tsx`

**Why**: Removes mock dependency, creates reusable utilities

---

### 6.2 Authentication Abstraction 🔴 **CRITICAL**
**Priority**: HIGH | **Effort**: 1-2 days | **Risk**: 🟡 Medium

**Problem**: Current implementation is Auth0-only, cannot support:
- Azure Digital Twins (requires MSAL)
- Self-hosted Konnektr (may use different OAuth)
- Generic OAuth PKCE flow

**Solution**: Pluggable authentication system with multiple providers

**Architecture**:
```
src/services/auth/
  ├── types.ts                    # AuthConfig, AuthContextValue, TokenCredentialProvider
  ├── AuthProvider.tsx            # Generic context provider (runtime provider selection)
  ├── TokenCredentialFactory.ts   # Creates Azure SDK credentials from auth context
  ├── providers/
  │   ├── Auth0Provider.tsx       # Auth0 PKCE (Konnektr hosted)
  │   ├── MsalProvider.tsx        # MSAL PKCE (Azure Digital Twins)
  │   └── GenericOAuthProvider.tsx # Generic OAuth PKCE (self-hosted)
  └── hooks/
      ├── useAuth.ts              # Generic auth hook (replaces useAuth0)
      └── useDigitalTwinsClient.ts # Updated to use generic auth
```

**Environment Examples**:
```bash
# Konnektr hosted (Auth0)
VITE_AUTH_PROVIDER=auth0
VITE_AUTH0_DOMAIN=auth.konnektr.io
VITE_AUTH0_CLIENT_ID=xxx
VITE_AUTH0_AUDIENCE=https://api.graph.konnektr.io

# Azure Digital Twins (MSAL)
VITE_AUTH_PROVIDER=msal
VITE_MSAL_CLIENT_ID=xxx
VITE_MSAL_TENANT_ID=xxx
VITE_MSAL_SCOPES=https://digitaltwins.azure.net/.default

# Self-hosted (Generic OAuth)
VITE_AUTH_PROVIDER=oauth
VITE_OAUTH_ISSUER=https://auth.example.com
VITE_OAUTH_CLIENT_ID=xxx
VITE_OAUTH_AUDIENCE=https://api.example.com
```

**Tasks**:
- [ ] Create auth directory structure
- [ ] Implement `AuthProvider.tsx` with generic context
- [ ] Migrate Auth0 logic to `Auth0Provider.tsx`
- [ ] Implement `MsalProvider.tsx` for ADT
- [ ] Implement `GenericOAuthProvider.tsx`
- [ ] Create `TokenCredentialFactory.ts`
- [ ] Update `useDigitalTwinsClient` to use generic auth
- [ ] Update `main.tsx` for provider selection
- [ ] Test all three providers

**Acceptance Criteria**:
- ✅ Auth0 flow works (backward compatible)
- ✅ MSAL flow works with Azure ADT
- ✅ Generic OAuth flow works
- ✅ Token refresh works for all providers
- ✅ Runtime provider selection via env vars

---

### 6.3 Replace QueryStore Mock Data 🟡 HIGH PRIORITY
**Priority**: HIGH | **Effort**: 4-6 hours | **Risk**: 🔴 High

**Problem**: `executeQuery()` in queryStore.ts uses hardcoded mock data (lines 150-210)

**Tasks**:
- [ ] Implement real query execution via Azure SDK:
  ```typescript
  const client = getClient();
  const queryResult = client.queryTwins(query);
  const results = [];
  for await (const item of queryResult) {
    results.push(item);
  }
  ```
- [ ] Remove mock data routing logic
- [ ] Add query syntax validation
- [ ] Add proper error handling
- [ ] Update result transformation

**Testing**:
- [ ] SELECT queries
- [ ] MATCH queries
- [ ] JOIN queries
- [ ] Aggregations (AVG, COUNT)
- [ ] Nested results (COLLECT)
- [ ] Invalid query error handling

---

### 6.4 Replace ModelSidebar Mock Data
**Priority**: MEDIUM | **Effort**: 2-3 hours | **Risk**: 🟡 Medium

**Tasks**:
- [ ] Update to use `modelsStore.fetchModels()`
- [ ] Fetch twin counts per model from API
- [ ] Remove direct mock imports
- [ ] Add loading/error states

---

### 6.5 Replace ModelInspector Mock Data
**Priority**: LOW | **Effort**: 1 hour | **Risk**: 🟢 Low

**Tasks**:
- [ ] Update to use `modelsStore` instead of `mockModels`
- [ ] Handle loading state

---

### 6.6 End-to-End Testing with Real Backend 🔴 **CRITICAL**
**Priority**: HIGH | **Effort**: 1-2 days | **Risk**: 🟡 Medium

**Prerequisites**:
- [ ] AgeDigitalTwins API running
- [ ] Auth0 configured for dev environment
- [ ] Test data loaded in PostgreSQL/AGE

**Test Scenarios**:
1. **Authentication**: Login, token refresh, logout, expired token
2. **Connections**: Add, switch, delete connections
3. **Models**: Fetch, view, upload, delete
4. **Twins**: List, create, update, delete, view relationships
5. **Queries**: Execute all query types, view in all modes, export
6. **Inspector**: Click-to-inspect in table and graph
7. **Performance**: Query execution time, graph rendering

**Acceptance Criteria**:
- ✅ All features work with real backend
- ✅ No console errors
- ✅ Performance acceptable (<2s for queries)
- ✅ Error handling works correctly
- ✅ Zero mock data in production code

---

## Phase 7: Polish & Production Readiness (FUTURE)

### 7.1 User Experience Enhancements
- Keyboard shortcuts for common actions
- Query templates and examples
- Improved error messages with suggestions
- Loading states and progress indicators
- Undo/redo for queries

### 7.2 Performance Optimization
- Virtual scrolling for large result sets (>1000 rows)
- Lazy loading for graph nodes
- Query result caching
- Debounced user inputs
- Web Worker for graph layout calculations

### 7.3 Documentation
- Component API documentation
- User guide for query features
- Developer guide for extending views
- Architecture decision records
- Deployment guide

- Install Sigma.js and related dependencies
- Create graph visualization component
- Implement node/edge rendering for Digital Twins
- Add interactive features (zoom, pan, selection)
- Toggle between table and graph views

**New Files**:

- `src/components/visualization/GraphViewer.tsx` - Main graph component
- `src/components/visualization/GraphControls.tsx` - Graph interaction controls
- `src/hooks/useGraphData.ts` - Transform query results to graph format
- `src/utils/graphUtils.ts` - Graph layout and styling utilities

**Dependencies to Install**:

```bash
pnpm add sigma graphology graphology-layout-force graphology-layout-circular
pnpm add @types/sigma
```

### 4.2 Nested Query Results Enhancement

**Problem**: Complex queries return nested objects that need proper table display
**Reference**: `nested-results-patterns.tsx` shows three approaches:

1. **Grouped Columns**: Expandable column groups for related data
2. **Flat Columns**: Prefixed column names for nested properties
3. **Expandable Rows**: Master-detail view with expandable row content

**Implementation Strategy**:

- Analyze query result structure to detect nesting
- Implement dynamic column generation based on result schema
- Add column grouping and expansion controls
- Create expandable row details for complex objects

## Implementation Priorities

### ✅ Completed (Phase 1-5)
- ✅ **Phase 1-4**: Core UI, Monaco editor, Sigma.js, Inspector system
- ✅ **Phase 5.1**: Graph view data transformation
- ✅ **Phase 5.2**: Advanced table features (4 view modes)
- ✅ **Phase 5.3**: Component cleanup (QueryResultsImproved removed)
- ✅ Cookie consent + GTAG analytics

### 🔄 Current (Week 1-2) - Phase 6 Foundation
**Goal**: Prepare for real backend integration

1. **Phase 6.1**: Extract Reusable Helpers (2-4 hours) 🟢
   - Quick win, low risk
   - Removes mock dependencies
   - **Start here**

2. **Phase 6.2**: Authentication Abstraction (1-2 days) 🔴 **CRITICAL**
   - Blocks all backend testing
   - Enables multi-provider support (Auth0, MSAL, OAuth)
   - Medium risk but essential
   - **Critical path**

### Short Term (Week 3-4) - Phase 6 Completion
**Goal**: Replace all mock data

3. **Phase 6.3**: QueryStore Real Data (4-6 hours) 🔴
   - High risk, core functionality
   - Enables real query execution
   - Requires Phase 6.2 complete

4. **Phase 6.4-6.5**: Component Mock Removal (3-4 hours) 🟡
   - Medium risk, UI components
   - ModelSidebar, ModelInspector
   - Can parallelize with Phase 6.3

5. **Phase 6.6**: E2E Testing (1-2 days) 🔴 **CRITICAL**
   - Comprehensive backend validation
   - Performance testing
   - Production readiness check

### Medium Term (Month 2-3) - Phase 7 Polish
6. **Phase 7**: UX enhancements, performance optimization, documentation

## Testing Strategy

### Unit Tests Required
- `queryResultsTransformer.ts` - Data transformation logic
- Data structure detection utilities
- Type guards and validators

### Integration Tests Required
- QueryResults with all view modes
- Graph view with real query results
- Inspector integration across views
- Export functionality for all modes

### Manual Testing Checklist
- [ ] Execute various query types (SELECT, MATCH, JOIN)
- [ ] Switch between all view modes
- [ ] Test with flat, nested, and complex data structures
- [ ] Verify inspector populates correctly
- [ ] Test pagination with large result sets
- [ ] Export data in all formats
- [ ] Test graph interactions (pan, zoom, click)
- [ ] Verify error handling for invalid data

## Dependencies & Considerations

### Existing Dependencies (Keep)
- `sigma` - Graph visualization
- `graphology` - Graph data structure
- `@monaco-editor/react` - Code editor
- `zustand` - State management
- `react-resizable-panels` - Layout

### No New Dependencies Required
All Phase 5 work uses existing dependencies and infrastructure.

## Success Metrics

### ✅ Phase 5 Success Criteria (ACHIEVED)
- ✅ Graph view displays query results instead of mock data
- ✅ Handles 90%+ of common query result structures
- ✅ Graceful fallback for non-graph data
- ✅ All 4 table view modes functional
- ✅ Smart view detection works correctly
- ✅ Single, unified QueryResults component
- ✅ No duplicate components
- ✅ Type-safe throughout (no `any` types)
- ✅ Test coverage 100% for dataStructureDetector utility

### Phase 6 Success Criteria (TARGET)

#### 6.2 Authentication
- ✅ Support Auth0 (Konnektr hosted)
- ✅ Support MSAL (Azure Digital Twins)
- ✅ Support Generic OAuth (self-hosted)
- ✅ PKCE flow for all providers
- ✅ Token refresh works correctly
- ✅ Runtime provider selection via env vars
- ✅ No breaking changes to existing components

#### 6.3-6.5 Data Integration
- ✅ Zero mock data in production code
- ✅ All stores use real API calls
- ✅ All components fetch real data
- ✅ Proper loading/error states everywhere
- ✅ Performance: Queries < 2s, Graph render < 1s

#### 6.6 Testing
- ✅ All auth flows tested (Auth0, MSAL, OAuth)
- ✅ All CRUD operations work with real backend
- ✅ All query types execute correctly
- ✅ Graph visualization works with real data
- ✅ Inspector works across all scenarios
- ✅ Export functions work
- ✅ Error handling validated
- ✅ No console errors in production build
- ✅ Performance acceptable under load

### Phase 6 Overall Success
- ✅ Application works with real AgeDigitalTwins backend
- ✅ Application works with Azure Digital Twins (ADT)
- ✅ Application works with self-hosted instances
- ✅ Zero mock data in codebase (except test fixtures)
- ✅ Comprehensive E2E test coverage
- ✅ Production-ready deployment
- ✅ Documentation updated

## Related Documents

- **[QUERY_COMPONENTS_ANALYSIS.md](../src/frontend/QUERY_COMPONENTS_ANALYSIS.md)** - Detailed component analysis
- **[DEVELOPMENT_PROGRESS.md](../src/frontend/DEVELOPMENT_PROGRESS.md)** - Current status
- **[API_INTEGRATION_COMPLETE.md](./API_INTEGRATION_COMPLETE.md)** - API integration details

## Notes & Guidelines

### Development Principles
- **TypeScript Strictness**: No `any` types allowed (enforced by copilot-instructions)
- **Separation of Concerns**: Each store/component has single responsibility
- **Backward Compatibility**: Phase 6 changes maintain existing component APIs
- **Type Safety**: All new code must have explicit types
- **Testing**: Unit tests for utilities, integration tests for stores, E2E for features

### Key Architectural Decisions
1. **Zustand for State**: Chosen for simplicity and TypeScript support
2. **Azure SDK Client**: Maintains compatibility with Azure Digital Twins
3. **Pluggable Auth**: Supports multiple identity providers without code changes
4. **DTDL Compatibility**: Full support for Digital Twins Definition Language

### Breaking Changes
- **Phase 6.2 (Auth)**: May require environment variable updates for deployments
- **No Component API Changes**: All existing component interfaces remain stable

---

**Last Updated**: November 1, 2025  
**Current Phase**: Phase 6 (Real Backend Integration)  
**Next Milestone**: Phase 6.1 (Extract Helpers) + Phase 6.2 (Auth Abstraction)  
**Estimated Completion**: 2-3 weeks

## Implementation Priority

### Immediate (This Session)

1. **Fix resizable panel conflicts** - Critical UX issue
2. **Implement Digital Twin data models** - Foundation for all other features
3. **Add inspector panel integration** - Basic functionality completion

### Next Session

4. **Sigma.js graph viewer** - Major feature addition
5. **Nested results enhancement** - Complex data handling
6. **Auth0 integration** - Security foundation

### Future Sessions

7. **Backend API integration** - Real data connection
8. **Advanced query features** - Enhanced functionality
9. **Model visualization** - Complete feature set

## Technical Architecture

### State Management Strategy

```
workspaceStore - UI state, panel sizes, selections
connectionStore - Connection status, environment config
queryStore - Query execution, history, results
authStore - Authentication state, user info
modelStore - DTDL models, model tree
```

### Component Architecture

```
App
├── AuthProvider (Phase 5)
├── ConnectionProvider (Phase 5)
├── GraphHeader
├── PanelGroup
│   ├── ModelSidebar
│   ├── MainContent
│   │   ├── QueryExplorer
│   │   │   ├── MonacoEditor
│   │   │   ├── QueryResults
│   │   │   │   ├── TableView (enhanced)
│   │   │   │   ├── GraphView (Sigma.js)
│   │   │   │   └── RawView
│   │   │   └── QueryHistory
│   │   └── ModelGraphViewer (Phase 6)
│   └── InspectorPanel
│       ├── TwinInspector
│       ├── RelationshipInspector
│       └── ModelInspector
└── StatusBar
```

### API Integration Strategy

- **Compatible with Azure Digital Twins SDK** - Use same interfaces
- **Proxy pattern** - Route requests through local proxy to Konnektr Graph
- **Authentication integration** - Auth0 tokens in API requests
- **Error handling** - Consistent error display across components
- **Caching strategy** - Cache models and frequently accessed data

## Success Metrics

### Phase 3 Success

- [ ] Panel resizing works smoothly without conflicts
- [ ] Query results display real Digital Twin structure
- [ ] Clicking results populates inspector correctly
- [ ] All TypeScript errors resolved

### Phase 4 Success

- [ ] Graph visualization displays query results
- [ ] Nested query results display properly in table
- [ ] Users can switch between table/graph/raw views seamlessly

### Phase 5 Success

- [ ] Authentication flow works end-to-end
- [ ] API calls return real data from Konnektr Graph
- [ ] Connection management functional

### Phase 6 Success

- [ ] Complete feature parity with reference implementation
- [ ] Professional UX suitable for production use
- [ ] Performance optimized for large datasets

## Files Changed Summary

This development plan will involve creating approximately 25-30 new files and modifying 15-20 existing files. The changes are structured to maintain backward compatibility and allow for incremental testing at each phase.

The plan prioritizes critical fixes first, then builds upon the foundation to add advanced features. Each phase has clear deliverables and success criteria.
