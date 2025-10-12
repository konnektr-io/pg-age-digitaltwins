import React, { useState } from "react";
import {
  ChevronRight,
  ChevronDown,
  Table2,
  Columns,
  Rows,
  Download,
  Eye,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useInspectorStore } from "@/stores/inspectorStore";

interface QueryResultsImprovedProps {
  results: Record<string, unknown>[] | null;
  error: string | null;
  isLoading: boolean;
}

export function QueryResultsImproved({
  results,
  error,
  isLoading,
}: QueryResultsImprovedProps) {
  const [viewMode, setViewMode] = useState<"grouped" | "flat" | "expandable">(
    "grouped"
  );
  const [expandedColumns, setExpandedColumns] = useState<
    Record<string, boolean>
  >({});
  const [expandedRows, setExpandedRows] = useState<Set<number>>(new Set([0]));
  const { selectItem } = useInspectorStore();

  // Get entity columns from results
  const getEntityColumns = () => {
    if (!results || results.length === 0) return [];
    const firstRow = results[0];
    return Object.keys(firstRow).filter((key) => {
      const value = firstRow[key];
      return typeof value === "object" && value !== null && "$dtId" in value;
    });
  };

  const entityColumns = getEntityColumns();

  // Initialize expanded columns state
  React.useEffect(() => {
    if (entityColumns.length > 0) {
      const initialExpanded = entityColumns.reduce(
        (acc, col) => ({
          ...acc,
          [col]: true,
        }),
        {}
      );
      setExpandedColumns(initialExpanded);
    }
  }, [results]);

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

  const getEntityProperties = (entity: unknown) => {
    if (typeof entity !== "object" || entity === null) return [];
    const entries = Object.entries(entity);
    return entries.filter(([key]) => key !== "$metadata");
  };

  const getEntityType = (entity: unknown) => {
    if (
      typeof entity === "object" &&
      entity !== null &&
      "$metadata" in entity &&
      typeof entity.$metadata === "object" &&
      entity.$metadata !== null &&
      "$model" in entity.$metadata
    ) {
      const model = String(entity.$metadata.$model);
      const parts = model.split(":");
      return parts[parts.length - 1]?.split(";")[0] || "Entity";
    }
    return "Entity";
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

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-muted-foreground">Loading results...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-64 text-destructive">
        Error: {error}
      </div>
    );
  }

  if (!results || results.length === 0) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-muted-foreground">No results</div>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col">
      {/* View Mode Selector */}
      <div className="flex items-center justify-between p-4 border-b">
        <div className="flex gap-2">
          <Button
            variant={viewMode === "grouped" ? "default" : "outline"}
            size="sm"
            onClick={() => setViewMode("grouped")}
            className="flex items-center gap-2"
          >
            <Columns className="w-4 h-4" />
            Grouped Columns
          </Button>
          <Button
            variant={viewMode === "flat" ? "default" : "outline"}
            size="sm"
            onClick={() => setViewMode("flat")}
            className="flex items-center gap-2"
          >
            <Table2 className="w-4 h-4" />
            Flat Columns
          </Button>
          <Button
            variant={viewMode === "expandable" ? "default" : "outline"}
            size="sm"
            onClick={() => setViewMode("expandable")}
            className="flex items-center gap-2"
          >
            <Rows className="w-4 h-4" />
            Expandable Rows
          </Button>
        </div>

        <div className="flex items-center gap-2">
          <Badge variant="secondary" className="text-xs">
            {results.length} results
          </Badge>
          <Button variant="outline" size="sm">
            <Download className="w-4 h-4 mr-2" />
            Export
          </Button>
        </div>
      </div>

      {/* Grouped Columns View */}
      {viewMode === "grouped" && (
        <div className="flex-1 overflow-auto">
          <Table>
            <TableHeader>
              {/* Entity headers */}
              <TableRow>
                {entityColumns.map((entityKey) => {
                  const firstEntity = results[0][entityKey];
                  const entityType = getEntityType(firstEntity);
                  const properties = getEntityProperties(firstEntity);

                  return (
                    <TableHead
                      key={entityKey}
                      className="bg-muted/50 border-r border-border"
                      colSpan={
                        expandedColumns[entityKey] ? properties.length : 1
                      }
                    >
                      <button
                        className="flex items-center gap-1.5 hover:text-foreground w-full"
                        onClick={() => toggleColumn(entityKey)}
                      >
                        {expandedColumns[entityKey] ? (
                          <ChevronDown className="w-4 h-4" />
                        ) : (
                          <ChevronRight className="w-4 h-4" />
                        )}
                        <span className="font-mono text-sm">{entityKey}</span>
                        <span className="text-xs text-muted-foreground ml-1">
                          ({entityType})
                        </span>
                        <span className="ml-auto text-xs text-muted-foreground">
                          {expandedColumns[entityKey]
                            ? `${properties.length} cols`
                            : "collapsed"}
                        </span>
                      </button>
                    </TableHead>
                  );
                })}
              </TableRow>

              {/* Property headers */}
              <TableRow>
                {entityColumns.map((entityKey) => {
                  const firstEntity = results[0][entityKey];
                  const properties = getEntityProperties(firstEntity);

                  if (expandedColumns[entityKey]) {
                    return properties.map(([propKey], propIdx) => (
                      <TableHead
                        key={`${entityKey}-${propKey}`}
                        className={`text-xs font-medium ${
                          propIdx === properties.length - 1
                            ? "border-r border-border"
                            : "border-r border-border/30"
                        }`}
                      >
                        {propKey}
                      </TableHead>
                    ));
                  } else {
                    return (
                      <TableHead
                        key={entityKey}
                        className="text-xs font-medium border-r border-border"
                      >
                        <span className="text-muted-foreground italic">
                          {properties.length} properties hidden
                        </span>
                      </TableHead>
                    );
                  }
                })}
              </TableRow>
            </TableHeader>

            <TableBody>
              {results.map((row, rowIdx) => (
                <TableRow key={rowIdx} className="hover:bg-muted/50">
                  {entityColumns.map((entityKey) => {
                    const entity = row[entityKey];
                    const properties = getEntityProperties(entity);

                    if (expandedColumns[entityKey]) {
                      return properties.map(([propKey, propValue], propIdx) => (
                        <TableCell
                          key={`${entityKey}-${propKey}`}
                          className={`text-xs ${
                            propIdx === properties.length - 1
                              ? "border-r border-border"
                              : "border-r border-border/30"
                          }`}
                          onClick={() => handleEntityClick(entity, entityKey)}
                        >
                          {propKey === "$dtId" ? (
                            <code className="font-mono text-xs">
                              {String(propValue)}
                            </code>
                          ) : typeof propValue === "boolean" ? (
                            <Badge
                              variant={propValue ? "default" : "secondary"}
                              className="text-xs"
                            >
                              {propValue ? "true" : "false"}
                            </Badge>
                          ) : typeof propValue === "number" ? (
                            <span className="font-medium">
                              {propValue}
                              {propKey.toLowerCase().includes("temp") && "°C"}
                              {propKey.toLowerCase().includes("area") && "m²"}
                              {propKey.toLowerCase().includes("humidity") &&
                                "%"}
                            </span>
                          ) : (
                            <span>{String(propValue)}</span>
                          )}
                        </TableCell>
                      ));
                    } else {
                      const dtId = properties.find(
                        ([key]) => key === "$dtId"
                      )?.[1];
                      const name = properties.find(
                        ([key]) => key === "name"
                      )?.[1];

                      return (
                        <TableCell
                          key={entityKey}
                          className="text-muted-foreground text-xs border-r border-border cursor-pointer"
                          onClick={() => handleEntityClick(entity, entityKey)}
                        >
                          <TooltipProvider>
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <div className="flex items-center gap-1">
                                  <Eye className="w-3 h-3" />
                                  <span>
                                    {dtId} {name && `• ${name}`}
                                  </span>
                                </div>
                              </TooltipTrigger>
                              <TooltipContent>
                                <div className="text-xs">
                                  Click to expand or inspect
                                </div>
                              </TooltipContent>
                            </Tooltip>
                          </TooltipProvider>
                        </TableCell>
                      );
                    }
                  })}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Flat Columns View */}
      {viewMode === "flat" && (
        <div className="flex-1 overflow-auto">
          <Table>
            <TableHeader>
              <TableRow>
                {entityColumns.flatMap((entityKey) => {
                  const firstEntity = results[0][entityKey];
                  const properties = getEntityProperties(firstEntity);
                  return properties.map(([propKey], propIdx) => (
                    <TableHead
                      key={`${entityKey}.${propKey}`}
                      className={`text-xs font-medium ${
                        propIdx === 0 ? "border-l border-border" : ""
                      }`}
                    >
                      {entityKey}.{propKey}
                    </TableHead>
                  ));
                })}
              </TableRow>
            </TableHeader>
            <TableBody>
              {results.map((row, rowIdx) => (
                <TableRow key={rowIdx} className="hover:bg-muted/50">
                  {entityColumns.flatMap((entityKey) => {
                    const entity = row[entityKey];
                    const properties = getEntityProperties(entity);
                    return properties.map(([propKey, propValue], propIdx) => (
                      <TableCell
                        key={`${entityKey}.${propKey}`}
                        className={`text-xs ${
                          propIdx === 0 ? "border-l border-border" : ""
                        }`}
                        onClick={() => handleEntityClick(entity, entityKey)}
                      >
                        {propKey === "$dtId" ? (
                          <code className="font-mono text-xs">
                            {String(propValue)}
                          </code>
                        ) : typeof propValue === "boolean" ? (
                          <Badge
                            variant={propValue ? "default" : "secondary"}
                            className="text-xs"
                          >
                            {propValue ? "true" : "false"}
                          </Badge>
                        ) : typeof propValue === "number" ? (
                          <span className="font-medium">
                            {propValue}
                            {propKey.toLowerCase().includes("temp") && "°C"}
                            {propKey.toLowerCase().includes("area") && "m²"}
                            {propKey.toLowerCase().includes("humidity") && "%"}
                          </span>
                        ) : (
                          <span>{String(propValue)}</span>
                        )}
                      </TableCell>
                    ));
                  })}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Expandable Rows View */}
      {viewMode === "expandable" && (
        <div className="flex-1 overflow-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-8"></TableHead>
                <TableHead>Entity</TableHead>
                <TableHead>ID</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Type</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {results.flatMap((row, rowIdx) =>
                entityColumns.flatMap((entityKey, entityIdx) => {
                  const entity = row[entityKey];
                  const properties = getEntityProperties(entity);
                  const dtId = properties.find(([key]) => key === "$dtId")?.[1];
                  const name = properties.find(([key]) => key === "name")?.[1];
                  const entityType = getEntityType(entity);
                  const expandKey = `${rowIdx}-${entityIdx}`;

                  return [
                    <TableRow
                      key={expandKey}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => toggleRow(rowIdx)}
                    >
                      <TableCell>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-6 w-6 p-0"
                        >
                          {expandedRows.has(rowIdx) ? (
                            <ChevronDown className="w-4 h-4" />
                          ) : (
                            <ChevronRight className="w-4 h-4" />
                          )}
                        </Button>
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline" className="text-xs font-mono">
                          {entityKey}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <code className="font-mono text-xs">
                          {String(dtId)}
                        </code>
                      </TableCell>
                      <TableCell className="font-medium">
                        {String(name)}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {entityType}
                      </TableCell>
                    </TableRow>,
                    ...(expandedRows.has(rowIdx)
                      ? [
                          <TableRow key={`${expandKey}-expanded`}>
                            <TableCell></TableCell>
                            <TableCell colSpan={4} className="p-0">
                              <div className="p-4 bg-muted/30">
                                <div className="bg-background rounded-md p-3 border">
                                  <div className="flex items-center gap-2 mb-3">
                                    <Badge
                                      variant="outline"
                                      className="text-xs font-mono"
                                    >
                                      {entityKey}
                                    </Badge>
                                    <span className="text-sm font-semibold">
                                      {entityType} Properties
                                    </span>
                                  </div>
                                  <div className="grid grid-cols-2 gap-3 text-xs">
                                    {properties.map(([propKey, propValue]) => (
                                      <div key={propKey}>
                                        <span className="text-muted-foreground">
                                          {propKey}:
                                        </span>
                                        <span className="ml-2 font-medium">
                                          {propKey === "$dtId" ? (
                                            <code className="font-mono">
                                              {String(propValue)}
                                            </code>
                                          ) : typeof propValue === "boolean" ? (
                                            <Badge
                                              variant={
                                                propValue
                                                  ? "default"
                                                  : "secondary"
                                              }
                                              className="text-xs ml-2"
                                            >
                                              {propValue ? "true" : "false"}
                                            </Badge>
                                          ) : (
                                            String(propValue)
                                          )}
                                        </span>
                                      </div>
                                    ))}
                                  </div>
                                </div>
                              </div>
                            </TableCell>
                          </TableRow>,
                        ]
                      : []),
                  ];
                })
              )}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
