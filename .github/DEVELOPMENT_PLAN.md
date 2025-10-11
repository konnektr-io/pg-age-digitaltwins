# Konnektr Graph Explorer - Development Plan

## Current Status Overview

### ✅ Completed Features (Phase 1 & 2)

- **Core Layout & Navigation**: Resizable panels, header, sidebar, inspector
- **Monaco Editor**: Cypher syntax highlighting, IntelliSense, autocompletion
- **Query Results**: Basic table display with pagination and export
- **Query History**: Searchable history with metadata tracking
- **State Management**: Zustand stores for workspace, connection, and query state

### 🐛 Critical Issues Identified

1. **Resizable Panel Conflicts**: Panels interfere with each other during resize
2. **Data Model Mismatch**: Mock query results don't match Digital Twin structure
3. **Inspector Integration**: Clicking query results doesn't populate inspector
4. **Missing Graph Visualization**: Need Sigma.js graph viewer for visual results
5. **Nested Results**: Complex query results need proper table representation
6. **Authentication**: Need to integrate Auth0 authentication system
7. **Backend Integration**: Need to connect to actual Konnektr Graph API

## Phase 3: Critical Fixes & Core Integration

### 3.1 Fix Resizable Panel Issues

**Problem**: Panel resize interactions conflict with each other
**Solution**:

- Implement proper panel group isolation
- Add panel state management in workspace store
- Fix CSS conflicts in panel containers
- Test panel resize behavior across different screen sizes

**Files to Modify**:

- `App.tsx` - Fix PanelGroup configuration
- `workspaceStore.ts` - Add proper panel size state management
- Component CSS classes - Remove conflicting width constraints

### 3.2 Implement Digital Twin Data Models

**Problem**: Mock results don't match actual Digital Twin structure
**Solution**:

- Integrate provided TypeScript interfaces (`BasicDigitalTwin`, `BasicRelationship`, `DigitalTwinsModelData`)
- Update mock data to match real structure
- Create proper type definitions for query results

**New Files**:

- `src/types/DigitalTwin.ts` - Import and export all DT interfaces
- `src/types/QueryTypes.ts` - Define query result types
- `src/mocks/digitalTwinData.ts` - Proper mock data structure

**Files to Modify**:

- `queryStore.ts` - Update with proper DT types
- `QueryResults.tsx` - Handle DT-specific data display
- `ModelSidebar.tsx` - Use real model structure

### 3.3 Inspector Panel Integration

**Problem**: Query results don't populate inspector panel
**Solution**:

- Implement click handlers in query results table
- Create twin/relationship/model inspectors with proper DT data
- Add inspector state management for different data types

**New Files**:

- `src/components/inspector/TwinInspector.tsx` - Display twin details
- `src/components/inspector/RelationshipInspector.tsx` - Display relationship details
- `src/components/inspector/ModelInspector.tsx` - Display model details

**Files to Modify**:

- `QueryResults.tsx` - Add click handlers and selection state
- `InspectorPanel.tsx` - Route to appropriate inspector component
- `workspaceStore.ts` - Add selected item state management

## Phase 4: Graph Visualization & Advanced Results

### 4.1 Sigma.js Graph Viewer Implementation

**Requirements**:

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

**Files to Modify**:

- `QueryResults.tsx` - Add nested data handling logic
- `src/utils/queryResultsUtils.ts` - Helper functions for data transformation

### 4.3 Enhanced Query Results Tabs

**Requirements**:

- Add tab system to QueryResults component
- Tab 1: Table View (enhanced with nesting support)
- Tab 2: Graph View (Sigma.js visualization)
- Tab 3: Raw JSON View (for debugging)

## Phase 5: Authentication & Backend Integration

### 5.1 Auth0 Integration

**Reference Files**: `AuthSetup.tsx`, `LoginPage.tsx`, `callback.tsx`
**Requirements**:

- Install Auth0 dependencies
- Set up Auth0 provider and configuration
- Implement login/logout flow
- Add authentication guards to routes
- Integrate with API client authentication

**New Files**:

- `src/auth/AuthProvider.tsx` - Auth0 React provider setup
- `src/auth/AuthGuard.tsx` - Route protection component
- `src/auth/authConfig.ts` - Auth0 configuration
- `src/pages/LoginPage.tsx` - Login page component
- `src/pages/CallbackPage.tsx` - Auth callback handler

**Dependencies to Install**:

```bash
pnpm add @auth0/auth0-react @auth0/auth0-spa-js
```

### 5.2 API Client Integration

**Reference Files**: `clients.ts`, `TwinsApiService.ts`
**Requirements**:

- Create API client factory similar to reference implementation
- Implement Azure Digital Twins compatible API calls
- Add authentication headers and request interceptors
- Handle API errors and loading states

**New Files**:

- `src/api/ApiClient.ts` - Main API client factory
- `src/api/DigitalTwinsApi.ts` - Digital Twins API service
- `src/api/QueryApi.ts` - Query execution API
- `src/api/ModelApi.ts` - Model management API
- `src/hooks/useApi.ts` - React hooks for API calls

### 5.3 Connection Management

**Requirements**:

- Implement connection setup and validation
- Add environment/instance selection
- Store connection preferences
- Handle connection status and errors

**Files to Modify**:

- `connectionStore.ts` - Add real connection logic
- `GraphHeader.tsx` - Real connection status display
- `ModelSidebar.tsx` - Load real models from API

## Phase 6: Advanced Features & Polish

### 6.1 Query Builder Interface

**Requirements**:

- Visual query builder for non-technical users
- Drag-drop interface for building MATCH patterns
- Property and relationship filters
- Query validation and suggestions

### 6.2 Model Graph Visualization

**Requirements**:

- DTDL model relationship visualization
- Interactive model explorer
- Model inheritance tree display
- Model validation and editing

### 6.3 Real-time Features

**Requirements**:

- Live query results updates
- Real-time twin property changes
- WebSocket connection for live data
- Notification system for changes

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
