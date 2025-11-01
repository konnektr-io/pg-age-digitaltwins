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
} from "@/utils/dataStructureDetector";

interface FlatColumnsViewProps {
  results: unknown[];
  onEntityClick: (entity: unknown, entityKey: string) => void;
}

export function FlatColumnsView({
  results,
  onEntityClick,
}: FlatColumnsViewProps) {
  const entityColumns = getEntityColumns(results);

  return (
    <Table>
      <TableHeader>
        <TableRow>
          {entityColumns.flatMap((entityKey) => {
            const firstRow = results[0];
            if (typeof firstRow !== "object" || firstRow === null) return [];
            const firstEntity = (firstRow as Record<string, unknown>)[
              entityKey
            ];
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
        {results.map((row, rowIdx) => {
          if (typeof row !== "object" || row === null) return null;
          return (
            <TableRow key={rowIdx} className="hover:bg-muted/50">
              {entityColumns.flatMap((entityKey) => {
                const entity = (row as Record<string, unknown>)[entityKey];
                const properties = getEntityProperties(entity);
                return properties.map(([propKey, propValue], propIdx) => (
                  <TableCell
                    key={`${entityKey}.${propKey}`}
                    className={`text-xs cursor-pointer ${
                      propIdx === 0 ? "border-l border-border" : ""
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
              })}
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
