import { useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  Download,
  Eye,
  Table,
  Loader2,
  AlertCircle,
  Network,
  Type,
  Code,
  Columns,
  List,
  Rows,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { useInspectorStore } from "@/stores/inspectorStore";
import { GraphViewer } from "@/components/graph/GraphViewer";
import { transformResultsToGraph } from "@/utils/queryResultsTransformer";
import {
  analyzeDataStructure,
  type TableViewMode,
} from "@/utils/dataStructureDetector";
import {
  SimpleTableView,
  GroupedColumnsView,
  FlatColumnsView,
  ExpandableRowsView,
} from "./table-views";

interface QueryResultsProps {
  results: unknown[] | null;
  error: string | null;
  isLoading: boolean;
}

export function QueryResults({ results, error, isLoading }: QueryResultsProps) {
  const [viewMode, setViewMode] = useState<"table" | "raw" | "graph">("table");
  const [columnMode, setColumnMode] = useState<"display" | "raw">("display");

  // Analyze data structure and set smart default for table view mode
  const dataStructure = results ? analyzeDataStructure(results) : null;
  const [tableViewMode, setTableViewMode] = useState<TableViewMode>(
    dataStructure?.recommendedView ?? "simple"
  );

  // Update table view mode when results change
  useState(() => {
    if (dataStructure) {
      setTableViewMode(dataStructure.recommendedView);
    }
  });

  // Transform results for graph view
  const graphData = transformResultsToGraph(results);

  // Get column headers based on mode
  const getColumnHeaders = () => {
    if (!results || results.length === 0) return [];
    const firstRow = results[0];
    if (typeof firstRow !== "object" || firstRow === null) return [];
    const keys = Object.keys(firstRow);
    if (columnMode === "display") {
      // Try to get display names from DTDL for each key
      return keys.map((key) => {
        if (key.startsWith("$")) return key;
        return (
          key.charAt(0).toUpperCase() + key.slice(1).replace(/([A-Z])/g, " $1")
        );
      });
    }
    return keys;
  };

  const columnHeaders = getColumnHeaders();
  const columnKeys =
    results &&
    results.length > 0 &&
    typeof results[0] === "object" &&
    results[0] !== null
      ? Object.keys(results[0])
      : [];
  const [currentPage, setCurrentPage] = useState(1);
  const [expandedColumns, setExpandedColumns] = useState<
    Record<string, boolean>
  >({});
  const [expandedRows, setExpandedRows] = useState<Set<number>>(new Set());
  const pageSize = 50;
  const { selectItem } = useInspectorStore();

  const toggleColumn = (columnName: string) => {
    setExpandedColumns((prev) => ({
      ...prev,
      [columnName]: !prev[columnName],
    }));
  };

  const toggleRow = (idx: number) => {
    const newExpanded = new Set(expandedRows);
    if (newExpanded.has(idx)) {
      newExpanded.delete(idx);
    } else {
      newExpanded.add(idx);
    }
    setExpandedRows(newExpanded);
  };

  const handleEntityClick = (entity: unknown, entityKey: string) => {
    if (typeof entity === "object" && entity !== null && "$dtId" in entity) {
      selectItem({
        type: entityKey.toLowerCase() as "twin" | "relationship" | "model",
        id: String(entity.$dtId),
        data: entity,
      });
    }
  };

  const totalPages = results ? Math.ceil(results.length / pageSize) : 0;
  const paginatedResults = results
    ? results.slice((currentPage - 1) * pageSize, currentPage * pageSize)
    : [];

  // Function to determine item type and handle clicks
  const handleRowClick = (item: unknown) => {
    if (typeof item === "object" && item !== null) {
      const obj = item as Record<string, unknown>;
      if (typeof obj.$dtId === "string" && obj.$metadata) {
        selectItem({
          type: "twin",
          id: obj.$dtId,
          data: obj,
        });
        return;
      }
      if (
        typeof obj.$relationshipId === "string" &&
        typeof obj.$sourceId === "string" &&
        typeof obj.$targetId === "string"
      ) {
        selectItem({
          type: "relationship",
          id: obj.$relationshipId,
          data: obj,
        });
        return;
      }
      if (typeof obj["@id"] === "string" && obj["@id"].startsWith("dtmi:")) {
        selectItem({
          type: "model",
          id: obj["@id"],
          data: obj,
        });
        return;
      }
    }
    if (typeof item === "string" && item.startsWith("dtmi:")) {
      selectItem({
        type: "model",
        id: item,
        data: item,
      });
      return;
    }
  };

  const handleExport = () => {
    if (!results) return;
    const firstRow = results[0];
    if (typeof firstRow !== "object" || firstRow === null) return;
    const headers = Object.keys(firstRow);
    const csv = results
      .map((row) => {
        if (typeof row !== "object" || row === null) return "";
        return headers
          .map((key) => {
            const val = (row as Record<string, unknown>)[key];
            if (typeof val === "string") {
              return `"${val.replace(/"/g, '""')}"`;
            }
            return String(val ?? "");
          })
          .join(",");
      })
      .join("\n");
    const csvContent = headers.join(",") + "\n" + csv;
    const blob = new Blob([csvContent], { type: "text/csv" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `query-results-${Date.now()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="flex flex-col h-full bg-background border-t border-border">
      {/* Results Header */}
      <div className="flex items-center justify-between p-3 border-b border-border bg-card">
        <div className="flex items-center gap-3">
          <h3 className="font-semibold text-sm">Query Results</h3>
          {results && (
            <Badge variant="secondary" className="text-xs">
              {results.length} rows
            </Badge>
          )}
        </div>

        <div className="flex items-center gap-2">
          {/* View Mode Toggle */}
          <div className="flex gap-1 p-1 bg-muted rounded-md">
            <Button
              variant={viewMode === "table" ? "default" : "ghost"}
              size="sm"
              className="px-2 py-1 text-xs"
              onClick={() => setViewMode("table")}
            >
              <Table className="w-3 h-3" />
            </Button>
            <Button
              variant={viewMode === "graph" ? "default" : "ghost"}
              size="sm"
              className="px-2 py-1 text-xs"
              onClick={() => setViewMode("graph")}
            >
              <Network className="w-3 h-3" />
            </Button>
            <Button
              variant={viewMode === "raw" ? "default" : "ghost"}
              size="sm"
              className="px-2 py-1 text-xs"
              onClick={() => setViewMode("raw")}
            >
              <Eye className="w-3 h-3" />
            </Button>
          </div>

          {/* Column Mode Toggle - only show for table view */}
          {viewMode === "table" && (
            <>
              <div className="flex gap-1 p-1 bg-muted rounded-md">
                <Button
                  variant={columnMode === "display" ? "default" : "ghost"}
                  size="sm"
                  className="px-2 py-1 text-xs"
                  onClick={() => setColumnMode("display")}
                  title="Show display names from DTDL models"
                >
                  <Type className="w-3 h-3" />
                </Button>
                <Button
                  variant={columnMode === "raw" ? "default" : "ghost"}
                  size="sm"
                  className="px-2 py-1 text-xs"
                  onClick={() => setColumnMode("raw")}
                  title="Show raw field names"
                >
                  <Code className="w-3 h-3" />
                </Button>
              </div>

              {/* Table View Mode Toggle - only show if we have nested entities */}
              {dataStructure && dataStructure.hasNestedEntities && (
                <div className="flex gap-1 p-1 bg-muted rounded-md">
                  <Button
                    variant={tableViewMode === "simple" ? "default" : "ghost"}
                    size="sm"
                    className="px-2 py-1 text-xs"
                    onClick={() => setTableViewMode("simple")}
                    title="Simple table (nested data as JSON)"
                  >
                    <Table className="w-3 h-3" />
                  </Button>
                  <Button
                    variant={tableViewMode === "grouped" ? "default" : "ghost"}
                    size="sm"
                    className="px-2 py-1 text-xs"
                    onClick={() => setTableViewMode("grouped")}
                    title="Grouped columns (expandable column groups)"
                  >
                    <Columns className="w-3 h-3" />
                  </Button>
                  <Button
                    variant={tableViewMode === "flat" ? "default" : "ghost"}
                    size="sm"
                    className="px-2 py-1 text-xs"
                    onClick={() => setTableViewMode("flat")}
                    title="Flat columns (entity.property naming)"
                  >
                    <List className="w-3 h-3" />
                  </Button>
                  <Button
                    variant={
                      tableViewMode === "expandable" ? "default" : "ghost"
                    }
                    size="sm"
                    className="px-2 py-1 text-xs"
                    onClick={() => setTableViewMode("expandable")}
                    title="Expandable rows (master-detail view)"
                  >
                    <Rows className="w-3 h-3" />
                  </Button>
                </div>
              )}
            </>
          )}

          {/* Export Button */}
          <Button
            variant="outline"
            size="sm"
            onClick={handleExport}
            disabled={!results || results.length === 0}
            className="gap-2"
          >
            <Download className="w-3 h-3" />
            Export
          </Button>
        </div>
      </div>

      {/* Results Content */}
      <div className="flex-1 overflow-hidden">
        {isLoading && (
          <div className="flex items-center justify-center h-full">
            <div className="flex items-center gap-2 text-muted-foreground">
              <Loader2 className="w-4 h-4 animate-spin" />
              <span>Executing query...</span>
            </div>
          </div>
        )}

        {error && (
          <div className="flex items-center justify-center h-full p-4">
            <div className="max-w-2xl w-full">
              <div className="flex flex-col items-center gap-4 text-center">
                <div className="text-destructive">
                  <AlertCircle className="w-12 h-12 mx-auto mb-2" />
                  <div className="font-semibold text-lg">Query Failed</div>
                </div>
                <div className="w-full bg-muted p-4 rounded-lg text-left">
                  <div className="text-sm font-mono text-destructive break-words">
                    {error}
                  </div>
                </div>
                <div className="text-xs text-muted-foreground">
                  <p>Common issues:</p>
                  <ul className="list-disc list-inside mt-1 text-left">
                    <li>Check your connection settings</li>
                    <li>Verify authentication is working</li>
                    <li>Ensure the query syntax is correct</li>
                    <li>Check network connectivity</li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        )}

        {!isLoading && !error && !results && (
          <div className="flex items-center justify-center h-full text-muted-foreground">
            <div className="text-center">
              <Table className="w-8 h-8 mx-auto mb-2 opacity-50" />
              <p className="text-sm">Run a query to see results</p>
            </div>
          </div>
        )}

        {results && results.length === 0 && (
          <div className="flex items-center justify-center h-full text-muted-foreground">
            <div className="text-center">
              <Table className="w-8 h-8 mx-auto mb-2 opacity-50" />
              <p className="text-sm">Query returned no results</p>
            </div>
          </div>
        )}

        {results && results.length > 0 && (
          <>
            <ScrollArea className="flex-1">
              {viewMode === "table" ? (
                <div className="p-4">
                  {tableViewMode === "simple" && (
                    <SimpleTableView
                      results={paginatedResults}
                      columnKeys={columnKeys}
                      columnHeaders={columnHeaders}
                      onRowClick={handleRowClick}
                    />
                  )}
                  {tableViewMode === "grouped" && (
                    <GroupedColumnsView
                      results={paginatedResults}
                      expandedColumns={expandedColumns}
                      onToggleColumn={toggleColumn}
                      onEntityClick={handleEntityClick}
                    />
                  )}
                  {tableViewMode === "flat" && (
                    <FlatColumnsView
                      results={paginatedResults}
                      onEntityClick={handleEntityClick}
                    />
                  )}
                  {tableViewMode === "expandable" && (
                    <ExpandableRowsView
                      results={paginatedResults}
                      expandedRows={expandedRows}
                      onToggleRow={toggleRow}
                    />
                  )}
                </div>
              ) : viewMode === "graph" ? (
                <div className="p-4 h-full">
                  {graphData.hasGraphData ? (
                    <GraphViewer
                      twins={graphData.twins}
                      relationships={graphData.relationships}
                      onNodeClick={(twinId: string) =>
                        selectItem({ type: "twin", id: twinId })
                      }
                    />
                  ) : (
                    <div className="flex items-center justify-center h-full">
                      <div className="text-center max-w-md">
                        <Network className="w-12 h-12 mx-auto mb-4 opacity-50 text-muted-foreground" />
                        <h3 className="text-lg font-semibold mb-2">
                          No Graph Data Available
                        </h3>
                        <p className="text-sm text-muted-foreground mb-4">
                          The current query results don't contain digital twins
                          or relationships that can be visualized as a graph.
                        </p>
                        <p className="text-xs text-muted-foreground">
                          Graph view requires results with{" "}
                          <code className="bg-muted px-1 py-0.5 rounded">
                            $dtId
                          </code>{" "}
                          (twins) or{" "}
                          <code className="bg-muted px-1 py-0.5 rounded">
                            $relationshipId
                          </code>{" "}
                          (relationships) properties.
                        </p>
                      </div>
                    </div>
                  )}
                </div>
              ) : (
                <div className="p-4">
                  <pre className="bg-muted p-4 rounded-md text-xs font-mono overflow-auto">
                    {JSON.stringify(paginatedResults, null, 2)}
                  </pre>
                </div>
              )}
            </ScrollArea>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between p-3 border-t border-border bg-card">
                <div className="text-xs text-muted-foreground">
                  Showing {(currentPage - 1) * pageSize + 1} to{" "}
                  {Math.min(currentPage * pageSize, results.length)} of{" "}
                  {results.length} rows
                </div>

                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setCurrentPage(Math.max(1, currentPage - 1))}
                    disabled={currentPage <= 1}
                  >
                    <ChevronLeft className="w-3 h-3" />
                  </Button>

                  <span className="text-xs text-muted-foreground">
                    Page {currentPage} of {totalPages}
                  </span>

                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      setCurrentPage(Math.min(totalPages, currentPage + 1))
                    }
                    disabled={currentPage >= totalPages}
                  >
                    <ChevronRight className="w-3 h-3" />
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
