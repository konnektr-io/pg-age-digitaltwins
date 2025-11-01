import { ChevronDown, ChevronRight } from "lucide-react";
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
  getEntityColumns,
  getEntityProperties,
  getEntityType,
} from "@/utils/dataStructureDetector";

interface GroupedColumnsViewProps {
  results: unknown[];
  expandedColumns: Record<string, boolean>;
  onToggleColumn: (columnName: string) => void;
  onEntityClick: (entity: unknown, entityKey: string) => void;
}

export function GroupedColumnsView({
  results,
  expandedColumns,
  onToggleColumn,
  onEntityClick,
}: GroupedColumnsViewProps) {
  const entityColumns = getEntityColumns(results);

  return (
    <Table>
      <TableHeader>
        {/* Entity headers with expand/collapse */}
        <TableRow>
          {entityColumns.map((entityKey) => {
            const firstRow = results[0];
            if (typeof firstRow !== "object" || firstRow === null) return null;
            const firstEntity = (firstRow as Record<string, unknown>)[
              entityKey
            ];
            const entityType = getEntityType(firstEntity);
            const properties = getEntityProperties(firstEntity);
            const isExpanded = expandedColumns[entityKey] ?? false;

            return (
              <TableHead
                key={entityKey}
                className="bg-muted/50 border-r border-border"
                colSpan={isExpanded ? properties.length : 1}
              >
                <button
                  className="flex items-center gap-1.5 hover:text-foreground w-full"
                  onClick={() => onToggleColumn(entityKey)}
                >
                  {isExpanded ? (
                    <ChevronDown className="w-4 h-4" />
                  ) : (
                    <ChevronRight className="w-4 h-4" />
                  )}
                  <span className="font-mono text-sm">{entityKey}</span>
                  <span className="text-xs text-muted-foreground ml-1">
                    ({entityType})
                  </span>
                  <span className="ml-auto text-xs text-muted-foreground">
                    {isExpanded ? `${properties.length} cols` : "collapsed"}
                  </span>
                </button>
              </TableHead>
            );
          })}
        </TableRow>

        {/* Property headers */}
        <TableRow>
          {entityColumns.map((entityKey) => {
            const firstRow = results[0];
            if (typeof firstRow !== "object" || firstRow === null) return null;
            const firstEntity = (firstRow as Record<string, unknown>)[
              entityKey
            ];
            const properties = getEntityProperties(firstEntity);
            const isExpanded = expandedColumns[entityKey] ?? false;

            if (isExpanded) {
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
        {results.map((row, rowIdx) => {
          if (typeof row !== "object" || row === null) return null;
          return (
            <TableRow key={rowIdx} className="hover:bg-muted/50">
              {entityColumns.map((entityKey) => {
                const entity = (row as Record<string, unknown>)[entityKey];
                const properties = getEntityProperties(entity);
                const isExpanded = expandedColumns[entityKey] ?? false;

                if (isExpanded) {
                  return properties.map(([propKey, propValue], propIdx) => (
                    <TableCell
                      key={`${entityKey}-${propKey}`}
                      className={`text-xs cursor-pointer ${
                        propIdx === properties.length - 1
                          ? "border-r border-border"
                          : "border-r border-border/30"
                      }`}
                      onClick={() => onEntityClick(entity, entityKey)}
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
                        <span className="font-medium">{propValue}</span>
                      ) : (
                        <span>{String(propValue)}</span>
                      )}
                    </TableCell>
                  ));
                } else {
                  const dtId = properties.find(([key]) => key === "$dtId")?.[1];
                  return (
                    <TableCell
                      key={entityKey}
                      className="text-muted-foreground text-xs border-r border-border cursor-pointer"
                      onClick={() => onEntityClick(entity, entityKey)}
                    >
                      <span>{String(dtId)}</span>
                    </TableCell>
                  );
                }
              })}
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
