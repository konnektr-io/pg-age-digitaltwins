# Table View Modes - Visual Guide

## Overview

QueryResults now supports four different table view modes, each optimized for different data structures and use cases.

## 🔍 When Each Mode Appears

### Automatic Detection

The system analyzes your query results and recommends the best view mode:

```typescript
// Data structure is analyzed
const dataStructure = analyzeDataStructure(results);

// Recommendation logic:
if (!hasNestedEntities) → "simple"
else if (totalColumns > 15) → "flat"
else if (hasDeepNesting) → "expandable"
else → "grouped"
```

### Manual Override

If your results contain nested entities, a view mode selector appears:

- **Table icon** - Simple
- **Columns icon** - Grouped
- **List icon** - Flat
- **Rows icon** - Expandable

## 📊 View Mode Details

### 1. Simple Table View

**Best for**: Basic queries without nested entities

**Example Query**:

```sql
SELECT $dtId, name, temperature FROM DIGITALTWINS
```

**Display**:

```
┌──────────────┬────────────┬─────────────┐
│ $dtId        │ name       │ temperature │
├──────────────┼────────────┼─────────────┤
│ room-101     │ Room 101   │ 22.5        │
│ room-102     │ Room 102   │ 23.1        │
└──────────────┴────────────┴─────────────┘
```

**Features**:

- Standard table layout
- Nested data shown as JSON strings
- Click row to inspect
- Fastest rendering

---

### 2. Grouped Columns View

**Best for**: Queries with multiple related entities

**Example Query**:

```sql
SELECT Twin, Floor, Building
FROM DIGITALTWINS Twin
JOIN Floor RELATED Twin.locatedIn
JOIN Building RELATED Floor.locatedIn
```

**Display (Collapsed)**:

```
┌─────────────────────────┬─────────────────────────┬─────────────────────────┐
│ ˃ Twin (Room)           │ ˃ Floor (Floor)         │ ˃ Building (Building)   │
│   3 cols collapsed      │   4 cols collapsed      │   5 cols collapsed      │
├─────────────────────────┼─────────────────────────┼─────────────────────────┤
│ room-101 • Room 101     │ floor-1 • First Floor   │ bldg-a • Building A     │
│ room-102 • Room 102     │ floor-1 • First Floor   │ bldg-a • Building A     │
└─────────────────────────┴─────────────────────────┴─────────────────────────┘
```

**Display (Expanded - Twin)**:

```
┌──────────────┬────────────┬──────────────┬─────────────────────────┬─────────────────────────┐
│ ˅ Twin (Room)                            │ ˃ Floor (Floor)         │ ˃ Building (Building)   │
│   3 cols                                 │   4 cols collapsed      │   5 cols collapsed      │
├──────────────┬────────────┬──────────────┼─────────────────────────┼─────────────────────────┤
│ $dtId        │ name       │ temperature  │                         │                         │
├──────────────┼────────────┼──────────────┼─────────────────────────┼─────────────────────────┤
│ room-101     │ Room 101   │ 22.5         │ floor-1 • First Floor   │ bldg-a • Building A     │
│ room-102     │ Room 102   │ 23.1         │ floor-1 • First Floor   │ bldg-a • Building A     │
└──────────────┴────────────┴──────────────┴─────────────────────────┴─────────────────────────┘
```

**Features**:

- Two-tier headers (entity + properties)
- Click header to expand/collapse
- Shows entity type and column count
- Visual hierarchy with borders
- Click any cell to inspect

**Use Cases**:

- JOIN queries with multiple entities
- Related entities visualization
- Hierarchical data exploration

---

### 3. Flat Columns View

**Best for**: Wide tables, data export workflows

**Example Query**: (Same JOIN query as Grouped)

**Display**:

```
┌──────────────┬──────────────┬──────────────────┬─────────────┬──────────────┬───────────────┬───────────────────┬─────────────────┬──────────────────┬─────────────────────┐
│ Twin.$dtId   │ Twin.name    │ Twin.temperature │ Floor.$dtId │ Floor.name   │ Floor.number  │ Building.$dtId    │ Building.name   │ Building.address │ Building.floors     │
├──────────────┼──────────────┼──────────────────┼─────────────┼──────────────┼───────────────┼───────────────────┼─────────────────┼──────────────────┼─────────────────────┤
│ room-101     │ Room 101     │ 22.5             │ floor-1     │ First Floor  │ 1             │ bldg-a            │ Building A      │ 123 Main St      │ 5                   │
│ room-102     │ Room 102     │ 23.1             │ floor-1     │ First Floor  │ 1             │ bldg-a            │ Building A      │ 123 Main St      │ 5                   │
└──────────────┴──────────────┴──────────────────┴─────────────┴──────────────┴───────────────┴───────────────────┴─────────────────┴──────────────────┴─────────────────────┘
```

**Features**:

- All properties visible at once
- Prefixed column names (entity.property)
- No interaction needed
- Scrollable horizontally
- Great for CSV export
- Click any cell to inspect

**Use Cases**:

- Data export/analysis
- SQL-like flat table view
- Maximum data visibility
- Wide screen displays

---

### 4. Expandable Rows View

**Best for**: Deep inspection, many properties per entity

**Example Query**: (Same JOIN query as Grouped)

**Display (Collapsed)**:

```
┌────┬───────────────┬──────────────┬────────────────┐
│    │ Entity        │ ID           │ Type           │
├────┼───────────────┼──────────────┼────────────────┤
│ ˃  │ Twin          │ room-101     │ Room           │
│ ˃  │ Floor         │ floor-1      │ Floor          │
│ ˃  │ Building      │ bldg-a       │ Building       │
│ ˃  │ Twin          │ room-102     │ Room           │
│ ˃  │ Floor         │ floor-1      │ Floor          │
│ ˃  │ Building      │ bldg-a       │ Building       │
└────┴───────────────┴──────────────┴────────────────┘
```

**Display (Expanded - Twin row)**:

```
┌────┬───────────────┬──────────────┬────────────────┐
│    │ Entity        │ ID           │ Type           │
├────┼───────────────┼──────────────┼────────────────┤
│ ˅  │ Twin          │ room-101     │ Room           │
├────┴───────────────┴──────────────┴────────────────┤
│    ┌──────────────────────────────────────────────┐│
│    │ Twin - Room Properties                       ││
│    ├────────────────────┬─────────────────────────┤│
│    │ $dtId: room-101    │ name: Room 101          ││
│    │ temperature: 22.5  │ humidity: 45            ││
│    │ occupied: true     │ capacity: 10            ││
│    └────────────────────┴─────────────────────────┘│
├─────────────────────────────────────────────────────┤
│ ˃  │ Floor         │ floor-1      │ Floor          │
│ ˃  │ Building      │ bldg-a       │ Building       │
└────┴───────────────┴──────────────┴────────────────┘
```

**Features**:

- Compact main view (4 columns)
- Click row to expand details
- Properties shown in grid layout
- Styled detail panel
- Great for mobile/small screens
- Click any cell to inspect

**Use Cases**:

- Many properties per entity (> 10)
- Mobile/tablet viewing
- Focused entity inspection
- Drill-down workflows

---

## 🎯 Quick Reference

| View Mode  | Icon    | Best When                         | Columns Width | Interaction Required |
| ---------- | ------- | --------------------------------- | ------------- | -------------------- |
| Simple     | Table   | No nested entities                | Narrow        | None                 |
| Grouped    | Columns | Multiple entities, moderate width | Medium        | Click headers        |
| Flat       | List    | Many columns, wide display        | Very wide     | None (scroll)        |
| Expandable | Rows    | Many properties, vertical space   | Narrow        | Click rows           |

## 💡 Pro Tips

### Keyboard Shortcuts (Future Enhancement)

- **G** - Switch to Grouped view
- **F** - Switch to Flat view
- **E** - Switch to Expandable view
- **S** - Switch to Simple view

### Performance

- Simple view: Fastest (no complexity)
- Grouped view: Medium (dynamic columns)
- Flat view: Medium (many columns)
- Expandable view: Slowest (dynamic rows)

### Export Recommendations

- **CSV Export**: Use Flat view (all columns visible)
- **JSON Export**: Use Simple or Raw view
- **Report Generation**: Use Grouped or Expandable

### Accessibility

- All views support click-to-inspect
- Expandable views announce state changes
- Keyboard navigation supported
- Screen reader compatible

---

## 🔄 View Mode Transitions

```
User runs query
    ↓
System analyzes data structure
    ↓
┌─────────────────────────────────────┐
│ Has nested entities?                │
└──────┬──────────────────────┬───────┘
       No                     Yes
       ↓                       ↓
   Simple View         Show view selector
                              ↓
                    ┌─────────┴─────────┐
                    │ User can choose:  │
                    │ • Grouped (rec.)  │
                    │ • Flat            │
                    │ • Expandable      │
                    │ • Simple          │
                    └───────────────────┘
```

## 📱 Responsive Behavior

### Desktop (> 1200px)

- All views work well
- Flat view ideal for wide screens
- Grouped view provides good balance

### Tablet (768px - 1200px)

- Grouped view recommended
- Flat view may require horizontal scroll
- Expandable view works well

### Mobile (< 768px)

- Expandable view recommended
- Simple view acceptable
- Grouped/Flat may be hard to use

---

_This guide helps you understand when and how to use each table view mode for optimal data visualization and inspection._
