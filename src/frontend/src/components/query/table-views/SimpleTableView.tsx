import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

interface SimpleTableViewProps {
  results: unknown[];
  columnKeys: string[];
  columnHeaders: string[];
  onRowClick: (row: unknown) => void;
}

export function SimpleTableView({
  results,
  columnKeys,
  columnHeaders,
  onRowClick,
}: SimpleTableViewProps) {
  return (
    <Table>
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
        {results.map((row, index) => (
          <TableRow
            key={index}
            className="cursor-pointer hover:bg-muted/50 transition-colors"
            onClick={() => onRowClick(row)}
          >
            {columnKeys.map((key) => {
              let value: unknown = undefined;
              if (typeof row === "object" && row !== null) {
                value = (row as Record<string, unknown>)[key];
              }
              return (
                <TableCell key={key} className="font-mono text-xs">
                  {typeof value === "object" && value !== null
                    ? JSON.stringify(value)
                    : String(value)}
                </TableCell>
              );
            })}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
