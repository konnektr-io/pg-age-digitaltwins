import { ChevronDown, ChevronRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
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

interface ExpandableRowsViewProps {
  results: unknown[];
  expandedRows: Set<number>;
  onToggleRow: (rowIdx: number) => void;
}

export function ExpandableRowsView({
  results,
  expandedRows,
  onToggleRow,
}: ExpandableRowsViewProps) {
  const entityColumns = getEntityColumns(results);

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="w-8"></TableHead>
          <TableHead>Entity</TableHead>
          <TableHead>ID</TableHead>
          <TableHead>Type</TableHead>
        </TableRow>
      </TableHeader>

      <TableBody>
        {results.flatMap((row, rowIdx) => {
          if (typeof row !== "object" || row === null) return [];
          return entityColumns.flatMap((entityKey, entityIdx) => {
            const entity = (row as Record<string, unknown>)[entityKey];
            const properties = getEntityProperties(entity);
            const dtId = properties.find(([key]) => key === "$dtId")?.[1];
            const entityType = getEntityType(entity);
            const expandKey = `${rowIdx}-${entityIdx}`;
            const isExpanded = expandedRows.has(rowIdx);

            return [
              <TableRow
                key={expandKey}
                className="cursor-pointer hover:bg-muted/50"
                onClick={() => onToggleRow(rowIdx)}
              >
                <TableCell>
                  <Button variant="ghost" size="sm" className="h-6 w-6 p-0">
                    {isExpanded ? (
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
                  <code className="font-mono text-xs">{String(dtId)}</code>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {entityType}
                </TableCell>
              </TableRow>,
              ...(isExpanded
                ? [
                    <TableRow key={`${expandKey}-expanded`}>
                      <TableCell></TableCell>
                      <TableCell colSpan={3} className="p-0">
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
                                          propValue ? "default" : "secondary"
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
          });
        })}
      </TableBody>
    </Table>
  );
}
