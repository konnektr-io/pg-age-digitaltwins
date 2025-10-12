import React from "react";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { Clock } from "lucide-react";
import type { DigitalTwinPropertyMetadata } from "@/types";

interface MetadataTooltipProps {
  children: React.ReactNode;
  metadata?: DigitalTwinPropertyMetadata;
  className?: string;
}

export function MetadataTooltip({
  children,
  metadata,
  className,
}: MetadataTooltipProps) {
  if (
    !metadata ||
    (!metadata.lastUpdateTime &&
      !metadata.sourceTime &&
      metadata.desiredValue === undefined)
  ) {
    return <>{children}</>;
  }

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div className={className || ""}>{children}</div>
        </TooltipTrigger>
        <TooltipContent side="top" className="max-w-xs">
          <div className="space-y-1.5 text-xs">
            {metadata.lastUpdateTime && (
              <div className="flex items-center gap-1.5">
                <Clock className="w-3 h-3" />
                <span className="text-muted-foreground">Last updated:</span>
                <span>
                  {new Date(metadata.lastUpdateTime).toLocaleString()}
                </span>
              </div>
            )}
            {metadata.sourceTime && (
              <div className="flex items-center gap-1.5">
                <Clock className="w-3 h-3" />
                <span className="text-muted-foreground">Source time:</span>
                <span>{new Date(metadata.sourceTime).toLocaleString()}</span>
              </div>
            )}
            {metadata.desiredValue !== undefined && (
              <div className="flex items-center gap-1.5">
                <span className="text-muted-foreground">Desired value:</span>
                <span className="font-mono">
                  {String(metadata.desiredValue)}
                </span>
              </div>
            )}
            {metadata.ackVersion !== undefined && (
              <div className="flex items-center gap-1.5">
                <span className="text-muted-foreground">Ack version:</span>
                <span>{metadata.ackVersion}</span>
              </div>
            )}
            {metadata.ackCode !== undefined && (
              <div className="flex items-center gap-1.5">
                <span className="text-muted-foreground">Ack code:</span>
                <span>{metadata.ackCode}</span>
              </div>
            )}
            {metadata.ackDescription && (
              <div className="flex items-center gap-1.5">
                <span className="text-muted-foreground">Ack description:</span>
                <span>{metadata.ackDescription}</span>
              </div>
            )}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
