# Query Components Analysis & Consolidation Plan

## 📊 Current State Assessment

### Existing Components

#### Query Explorer Variants

1. **QueryExplorer.tsx** (PRIMARY - Currently in use)

   - **Location**: `src/components/query/QueryExplorer.tsx`
   - **Status**: ✅ Active (imported by MainContent.tsx)
   - **Features**:
     - Monaco Editor with Cypher syntax highlighting
     - Query execution with loading states
     - Horizontal panel split (editor | results)
     - Optional query history sidebar
     - Save, format, and run query buttons
   - **Dependencies**: MonacoEditor, QueryResults, QueryHistory

2. **QueryExplorerSimple.tsx** (UNUSED)
   - **Location**: `src/components/query/QueryExplorerSimple.tsx`
   - **Status**: ❌ Not imported anywhere
   - **Features**:
     - Basic Textarea instead of Monaco
     - Simpler toolbar (Run, Save, History)
     - Uses same QueryResults component
   - **Purpose**: Appears to be a fallback or prototype

#### Query Results Variants

1. **QueryResults.tsx** (PRIMARY - Currently in use)

   - **Location**: `src/components/query/QueryResults.tsx`
   - **Status**: ✅ Active (imported by QueryExplorer variants)
   - **Features**:
     - **Three view modes**: Table, Graph, Raw JSON
     - Table view with pagination (50 rows per page)
     - Graph view using Sigma.js GraphViewer component
     - Click-to-inspect functionality (populates inspector panel)
     - Export to CSV
     - Display/Raw column mode toggle
     - Mock data integration for graph view
   - **Graph Integration**:
     ```tsx
     <GraphViewer
       twins={mockDigitalTwins}
       relationships={mockRelationships}
       onNodeClick={(twinId: string) =>
         selectItem({ type: "twin", id: twinId })
       }
     />
     ```
   - **Issues**:
     - Graph view uses hard-coded mock data instead of query results
     - No parsing of query results into twins/relationships for graph

2. **QueryResultsImproved.tsx** (UNUSED - Prototype)
   - **Location**: `src/components/query/QueryResultsImproved.tsx`
   - **Status**: ❌ Not imported anywhere
   - **Features**:
     - **Three advanced view modes**: Grouped Columns, Flat Columns, Expandable Rows
     - Nested entity handling (detects objects with `$dtId`)
     - Expandable column groups for complex structures
     - Master-detail expandable rows
     - Entity click handling for inspector
   - **Purpose**: Advanced nested query result patterns (from DEVELOPMENT_PLAN.md Phase 4.2)
   - **Type Safety**: Expects `Record<string, unknown>[]` instead of `unknown[]`

#### Graph Viewer

1. **GraphViewer.tsx** (ACTIVE)
   - **Location**: `src/components/graph/GraphViewer.tsx`
   - **Status**: ✅ Active (imported by QueryResults.tsx)
   - **Features**:
     - Sigma.js + Graphology for interactive graph visualization
     - Color-coded nodes by model type (Building, Room, Sensor, etc.)
     - Click-to-inspect functionality
     - Pan/zoom controls
     - Circular layout algorithm
     - Edge rendering for relationships
   - **Props**:
     ```tsx
     interface GraphViewerProps {
       twins: BasicDigitalTwin[];
       relationships: BasicRelationship[];
       onNodeClick?: (twinId: string) => void;
     }
     ```

### Current Component Relationships

```
MainContent.tsx
  └─> QueryExplorer.tsx (Monaco version)
        ├─> MonacoEditor.tsx
        ├─> QueryHistory.tsx
        └─> QueryResults.tsx
              └─> GraphViewer.tsx (with mock data)

[UNUSED]
  ├─> QueryExplorerSimple.tsx
  └─> QueryResultsImproved.tsx
```

## 🎯 Identified Issues

### Critical Problems

1. **Graph View Data Mismatch**

   - QueryResults passes hard-coded mock data to GraphViewer
   - Query results are not parsed/transformed into graph format
   - No connection between actual query results and graph visualization

2. **Duplicate Components**

   - QueryExplorerSimple is unused but still maintained
   - QueryResultsImproved contains advanced features but isn't integrated

3. **Type Inconsistency**

   - QueryResults accepts `unknown[]`
   - QueryResultsImproved expects `Record<string, unknown>[]`
   - No unified type handling for query results

4. **Feature Fragmentation**
   - Advanced nested result handling is in QueryResultsImproved but not used
   - Graph visualization exists but doesn't consume real query results
   - Column grouping/expansion only in unused component

### Minor Issues

1. **Code Duplication**

   - Similar click handlers in both QueryResults variants
   - Repeated inspector selection logic

2. **Incomplete Implementation**
   - Save query functionality is stubbed out
   - Format query exists but needs validation

## 💡 Consolidation Strategy

### Recommended Approach: Unified QueryResults Component

Create a single, comprehensive QueryResults component that combines:

- Current table view with pagination
- QueryResultsImproved's nested data handling
- Working GraphViewer integration with real data transformation
- Type-safe result handling

### Component Structure

```
QueryResults.tsx (Unified)
  ├─> View Modes
  │     ├─> Table View
  │     │     ├─> Simple Table (flat data)
  │     │     ├─> Grouped Columns (nested entities)
  │     │     ├─> Expandable Rows (master-detail)
  │     │     └─> Column mode toggle (display/raw)
  │     ├─> Graph View
  │     │     └─> GraphViewer with transformed data
  │     └─> Raw JSON View
  └─> Common Features
        ├─> Pagination
        ├─> Export (CSV/JSON)
        ├─> Inspector integration
        └─> Loading/error states
```

## 🚀 Implementation Plan

### Phase 1: Data Transformation Layer (HIGH PRIORITY)

**Goal**: Enable GraphViewer to consume real query results

**Tasks**:

1. Create `src/utils/queryResultsTransformer.ts`
   - Function to parse query results into twins/relationships
   - Detect result schema (flat vs nested)
   - Type guards for BasicDigitalTwin and BasicRelationship

```typescript
interface TransformedResults {
  twins: BasicDigitalTwin[];
  relationships: BasicRelationship[];
  hasGraphData: boolean;
}

export function transformResultsToGraph(results: unknown[]): TransformedResults;
```

2. Update QueryResults.tsx to use transformer
   - Replace mock data with transformed query results
   - Add fallback when results aren't graph-compatible
   - Show helpful message when graph view unavailable

**Files to Modify**:

- `src/components/query/QueryResults.tsx` (lines 309-315)
- Create `src/utils/queryResultsTransformer.ts`

### Phase 2: Merge Advanced Table Features (MEDIUM PRIORITY)

**Goal**: Integrate QueryResultsImproved's nested data handling

**Tasks**:

1. Add table view mode selector to QueryResults.tsx

   - Simple Table (current)
   - Grouped Columns (from QueryResultsImproved)
   - Expandable Rows (from QueryResultsImproved)

2. Extract reusable components from QueryResultsImproved:

   - `src/components/query/table-views/GroupedColumnsTable.tsx`
   - `src/components/query/table-views/FlatColumnsTable.tsx`
   - `src/components/query/table-views/ExpandableRowsTable.tsx`

3. Update QueryResults.tsx to conditionally render based on:
   - User-selected view mode
   - Data structure detection (simple vs nested)

**Files to Create**:

- `src/components/query/table-views/GroupedColumnsTable.tsx`
- `src/components/query/table-views/FlatColumnsTable.tsx`
- `src/components/query/table-views/ExpandableRowsTable.tsx`

**Files to Modify**:

- `src/components/query/QueryResults.tsx`

### Phase 3: Clean Up Unused Components (LOW PRIORITY)

**Goal**: Remove or archive duplicate/unused code

**Tasks**:

1. Archive QueryExplorerSimple.tsx

   - Move to `src/components/query/archive/` or delete
   - Document reason (replaced by Monaco version)

2. Remove QueryResultsImproved.tsx after extracting features

   - Ensure all features are migrated to QueryResults.tsx
   - Update any references (should be none)

3. Add component documentation
   - Document view modes and when to use them
   - Add JSDoc comments to main components

**Files to Archive/Delete**:

- `src/components/query/QueryExplorerSimple.tsx`
- `src/components/query/QueryResultsImproved.tsx` (after migration)

### Phase 4: Enhanced Type Safety (MEDIUM PRIORITY)

**Goal**: Consistent types across all query components

**Tasks**:

1. Create `src/types/QueryResults.ts`

   ```typescript
   export type QueryResult = Record<string, unknown>;
   export type QueryResults = QueryResult[];

   export interface QueryResultsMetadata {
     columns: string[];
     hasNestedEntities: boolean;
     hasGraphData: boolean;
   }
   ```

2. Update component props to use unified types
3. Add runtime type guards and validators

**Files to Create**:

- `src/types/QueryResults.ts`

**Files to Modify**:

- `src/components/query/QueryResults.tsx`
- `src/stores/queryStore.ts`

## 📋 View Mode Matrix

| View Mode              | Best For                       | Data Structure        | Inspector              | Export |
| ---------------------- | ------------------------------ | --------------------- | ---------------------- | ------ |
| **Table - Simple**     | Flat data, quick scanning      | No nesting            | ✅ Click rows          | CSV    |
| **Table - Grouped**    | Nested entities, relationships | Entity objects        | ✅ Click entities      | CSV    |
| **Table - Expandable** | Complex nested data            | Any nesting           | ✅ Click rows/entities | CSV    |
| **Graph**              | Visual relationships           | Twins + Relationships | ✅ Click nodes         | Image  |
| **Raw JSON**           | Debugging, full data           | Any                   | ❌                     | JSON   |

## 🔧 Technical Decisions

### Keep

- ✅ QueryExplorer.tsx (Monaco version) - Primary editor
- ✅ QueryResults.tsx - Expand with new features
- ✅ GraphViewer.tsx - Working Sigma.js implementation
- ✅ MonacoEditor.tsx - Advanced code editing
- ✅ QueryHistory.tsx - Useful feature

### Archive

- 🗄️ QueryExplorerSimple.tsx - Superseded by Monaco version

### Migrate & Remove

- 🔄 QueryResultsImproved.tsx - Extract features → QueryResults.tsx → Delete

## 🎨 User Experience Flow

### Current Experience (After Phase 1)

1. User writes query in Monaco editor
2. Clicks "Run Query"
3. Results appear in bottom panel
4. User switches between Table/Graph/Raw views
5. Graph view shows actual query results (not mock data)
6. Clicking table rows or graph nodes opens inspector

### Enhanced Experience (After Phase 2)

1. User writes query in Monaco editor
2. Clicks "Run Query"
3. Results appear with smart view selection:
   - Simple flat data → Simple table
   - Nested entities → Grouped columns table (auto-detected)
   - Graph queries → Graph view recommended
4. User can manually switch view modes
5. Advanced table options for complex nested data
6. Inspector works across all view modes

## 📝 Migration Checklist

- [ ] **Phase 1: Graph Data Transformation**

  - [ ] Create queryResultsTransformer.ts
  - [ ] Implement transformResultsToGraph()
  - [ ] Add type guards for twins/relationships
  - [ ] Update QueryResults.tsx graph view
  - [ ] Test with various query result shapes
  - [ ] Add fallback messaging

- [ ] **Phase 2: Advanced Table Views**

  - [ ] Extract GroupedColumnsTable component
  - [ ] Extract FlatColumnsTable component
  - [ ] Extract ExpandableRowsTable component
  - [ ] Add table view mode selector
  - [ ] Add data structure detection
  - [ ] Test nested entity handling
  - [ ] Update documentation

- [ ] **Phase 3: Cleanup**

  - [ ] Archive QueryExplorerSimple.tsx
  - [ ] Remove QueryResultsImproved.tsx
  - [ ] Update imports
  - [ ] Remove unused dependencies
  - [ ] Update DEVELOPMENT_PROGRESS.md

- [ ] **Phase 4: Type Safety**
  - [ ] Create QueryResults.ts types
  - [ ] Update component props
  - [ ] Add runtime validators
  - [ ] Update tests

## 🔗 Related Documents

- `DEVELOPMENT_PLAN.md` - Phase 4: Graph Visualization & Advanced Results
- `DEVELOPMENT_PROGRESS.md` - Current status (Phase 4 completed)
- `nested-results-patterns.tsx` - Original mockup reference
- `.github/copilot-instructions.md` - TypeScript strictness rules

## 🎯 Success Criteria

✅ **Phase 1 Complete When**:

- Graph view displays real query results
- No more hard-coded mock data in graph
- Graceful handling when query results aren't graph-compatible

✅ **Phase 2 Complete When**:

- All table view modes work seamlessly
- Nested entity handling matches QueryResultsImproved
- User can switch between simple/grouped/expandable views

✅ **All Phases Complete When**:

- Only one QueryExplorer component (Monaco version)
- Only one QueryResults component (unified)
- GraphViewer consumes real data
- No duplicate/unused components
- Type-safe throughout
- Inspector works across all view modes
- Export works for all view modes

---

**Last Updated**: 2025-01-21
**Status**: Analysis Complete - Ready for Implementation
