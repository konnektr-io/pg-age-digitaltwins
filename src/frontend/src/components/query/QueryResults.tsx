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
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Table as UITable,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useInspectorStore } from "@/stores/inspectorStore";
import { GraphViewer } from "@/components/graph/GraphViewer";
import { mockDigitalTwins, mockRelationships } from "@/mocks/digitalTwinData";

interface QueryResultsProps {
  results: unknown[] | null;
  error: string | null;
  isLoading: boolean;
}

export function QueryResults({ results, error, isLoading }: QueryResultsProps) {
  const [viewMode, setViewMode] = useState<"table" | "raw" | "graph">("table");
  const [columnMode, setColumnMode] = useState<"display" | "raw">("display");

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
  const pageSize = 50;
  const { selectItem } = useInspectorStore();

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
          <div className="flex items-center justify-center h-full">
            <div className="flex items-center gap-2 text-destructive">
              <AlertCircle className="w-4 h-4" />
              <span>Error: {error}</span>
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
                  <UITable>
                    <TableHeader>
                      <TableRow>
                        {columnKeys.map((key, index) => (
                          <TableHead key={key} className="font-semibold">
                            {columnHeaders[index]}
                          </TableHead>
                        ))}
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {paginatedResults.map((row, index) => (
                        <TableRow
                          key={index}
                          className="cursor-pointer hover:bg-muted/50 transition-colors"
                          onClick={() => handleRowClick(row)}
                        >
                          {columnKeys.map((key) => {
                            let value: unknown = undefined;
                            if (typeof row === "object" && row !== null) {
                              value = (row as Record<string, unknown>)[key];
                            }
                            return (
                              <TableCell
                                key={key}
                                className="font-mono text-xs"
                              >
                                {typeof value === "object" && value !== null
                                  ? JSON.stringify(value)
                                  : String(value)}
                              </TableCell>
                            );
                          })}
                        </TableRow>
                      ))}
                    </TableBody>
                  </UITable>
                </div>
              ) : viewMode === "graph" ? (
                <div className="p-4 h-full">
                  <GraphViewer
                    twins={mockDigitalTwins}
                    relationships={mockRelationships}
                    onNodeClick={(twinId: string) =>
                      selectItem({ type: "twin", id: twinId })
                    }
                  />
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
