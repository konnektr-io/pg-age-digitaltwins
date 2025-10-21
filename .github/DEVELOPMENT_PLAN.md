# Konnektr Graph Explorer - Development Plan

## Current Status Overview

### ✅ Completed Features (Phase 1-4)

- **Core Layout & Navigation**: Resizable panels, header, sidebar, inspector ✅
- **Monaco Editor**: Cypher syntax highlighting, IntelliSense, autocompletion ✅
- **Query Results**: Table display with pagination and export ✅
- **Query History**: Searchable history with metadata tracking ✅
- **State Management**: Zustand stores for workspace, connection, and query state ✅
- **API Integration**: Real Azure Digital Twins API calls in stores ✅
- **Digital Twins Client**: Mock token credential for development ✅
- **Sigma.js Graph Viewer**: Interactive graph visualization component ✅
- **Inspector System**: Click-to-inspect for twins, relationships, and models ✅
- **Multi-View Results**: Table, Graph, and Raw JSON views ✅

### 🚨 Critical Issues Discovered (Phase 4 Review)

**See [QUERY_COMPONENTS_ANALYSIS.md](../src/frontend/QUERY_COMPONENTS_ANALYSIS.md) for full analysis**

1. **Graph View Data Mismatch** (HIGH PRIORITY)
   - GraphViewer receives hard-coded mock data instead of query results
   - No transformation layer to parse query results into graph format
   - Graph view shows same data regardless of query execution

2. **Component Duplication** (MEDIUM PRIORITY)
   - QueryExplorerSimple.tsx exists but is unused (Monaco version is active)
   - QueryResultsImproved.tsx has advanced features but isn't integrated
   - Feature fragmentation between components

3. **Missing Advanced Table Features** (MEDIUM PRIORITY)
   - QueryResultsImproved.tsx has grouped columns, flat columns, and expandable rows
   - These features handle nested entity structures better
   - Not available in active QueryResults.tsx

4. **Type Inconsistency** (LOW PRIORITY)
   - QueryResults accepts `unknown[]`
   - QueryResultsImproved expects `Record<string, unknown>[]`

## Phase 5: Component Consolidation & Enhancement (CURRENT PHASE)

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

## Phase 6: Polish & Documentation (FUTURE)

### 6.1 User Experience Enhancements
- Keyboard shortcuts for common actions
- Query templates and examples
- Improved error messages with suggestions
- Loading states and progress indicators

### 6.2 Performance Optimization
- Virtual scrolling for large result sets
- Lazy loading for graph nodes
- Query result caching
- Debounced user inputs

### 6.3 Documentation
- Component API documentation
- User guide for query features
- Developer guide for extending views
- Architecture decision records

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

### Immediate (Week 1-2)
1. **Phase 5.1**: Graph View Data Transformation (HIGH PRIORITY)
   - Critical bug fix for graph visualization
   - Unblocks real graph usage
   - Estimated: 4-6 hours

### Short Term (Week 3-4)
2. **Phase 5.2**: Merge Advanced Table Features (MEDIUM PRIORITY)
   - Enhances user experience with complex data
   - Utilizes existing code from QueryResultsImproved
   - Estimated: 8-12 hours

3. **Phase 5.3**: Clean Up Duplicate Components (LOW PRIORITY)
   - Reduces technical debt
   - Improves maintainability
   - Estimated: 2-3 hours

### Medium Term (Month 2)
4. **Phase 5.4**: Type Safety Enhancement (LOW PRIORITY)
5. **Phase 6**: Authentication & Backend Integration
6. **Phase 7**: Advanced Features & Polish

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

### Phase 5.1 Success Criteria
- ✅ Graph view displays query results instead of mock data
- ✅ Handles 90%+ of common query result structures
- ✅ Graceful fallback for non-graph data
- ✅ No console errors or type errors
- ✅ Performance: < 500ms transformation for 1000 results

### Phase 5.2 Success Criteria
- ✅ All 4 table view modes functional
- ✅ Smart view detection works correctly
- ✅ User can manually switch views
- ✅ Inspector works in all views
- ✅ Export works in all views
- ✅ Performance: No lag with 500 rows visible

### Phase 5 Overall Success
- ✅ Single, unified QueryResults component
- ✅ No duplicate or unused components
- ✅ Type-safe throughout
- ✅ Comprehensive documentation
- ✅ Test coverage > 80% for new code

## Related Documents

- **[QUERY_COMPONENTS_ANALYSIS.md](../src/frontend/QUERY_COMPONENTS_ANALYSIS.md)** - Detailed component analysis
- **[DEVELOPMENT_PROGRESS.md](../src/frontend/DEVELOPMENT_PROGRESS.md)** - Current status
- **[API_INTEGRATION_COMPLETE.md](./API_INTEGRATION_COMPLETE.md)** - API integration details

## Notes

- Phases 1-4 are complete but revealed issues during review
- Phase 5 focuses on fixing discovered issues and consolidating features
- Phase 6+ are future enhancements (not blocking current functionality)
- All Phase 5 work maintains backward compatibility
- No breaking changes to existing stores or APIs

---

**Last Updated**: 2025-01-21  
**Current Phase**: Phase 5 (Component Consolidation & Enhancement)  
**Next Milestone**: Phase 5.1 - Graph View Data Transformation

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
