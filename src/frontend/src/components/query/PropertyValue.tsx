import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { Clock, Info } from "lucide-react";
import type { BasicDigitalTwin } from "@/types";
import { getPropertyMetadata } from "@/utils/dtdlHelpers";

interface PropertyValueProps {
  twin: BasicDigitalTwin;
  propertyName: string;
  value: unknown;
  showTooltip?: boolean;
  className?: string;
}

export function PropertyValue({
  twin,
  propertyName,
  value,
  showTooltip = true,
  className = "",
}: PropertyValueProps) {
  const metadata = getPropertyMetadata(twin, propertyName);

  let displayValue: string;
  if (typeof value === "object" && value !== null) {
    displayValue = JSON.stringify(value);
  } else if (
    typeof value === "string" ||
    typeof value === "number" ||
    typeof value === "boolean"
  ) {
    displayValue = String(value);
  } else if (value === null || value === undefined) {
    displayValue = "";
  } else {
    // fallback for other types (e.g., symbol, bigint, function)
    displayValue = String(value);
  }

  if (!showTooltip || !metadata) {
    return <span className={className}>{displayValue}</span>;
  }

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <span
            className={`${className} cursor-help border-b border-dotted border-muted-foreground/30`}
          >
            {displayValue}
          </span>
        </TooltipTrigger>
        <TooltipContent side="top" className="max-w-xs">
          <div className="space-y-2">
            <div className="font-medium text-xs">Property Metadata</div>
            {metadata.lastUpdateTime && (
              <div className="flex items-center gap-1.5 text-xs">
                <Clock className="w-3 h-3" />
                <span className="text-muted-foreground">Last Updated:</span>
                <span>
                  {new Date(metadata.lastUpdateTime).toLocaleString()}
                </span>
              </div>
            )}
            {metadata.sourceTime && (
              <div className="flex items-center gap-1.5 text-xs">
                <Info className="w-3 h-3" />
                <span className="text-muted-foreground">Source Time:</span>
                <span>{new Date(metadata.sourceTime).toLocaleString()}</span>
              </div>
            )}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
