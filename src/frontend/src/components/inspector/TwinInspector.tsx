import { FileText, Database, Clock, Zap } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import type { BasicDigitalTwin } from "@/types";
import { getModelDisplayName } from "@/mocks/digitalTwinData";

interface TwinInspectorProps {
  twinId: string;
}

export function TwinInspector({ twinId }: TwinInspectorProps) {
  // In a real app, this would fetch the twin data from the API
  // For now, we'll simulate loading the twin data
  const mockTwin: BasicDigitalTwin = {
    $dtId: twinId,
    $metadata: {
      $model: "dtmi:example:Room;1",
      name: {
        lastUpdateTime: "2024-01-15T10:32:00Z",
      },
      temperature: {
        lastUpdateTime: "2024-01-15T11:45:00Z",
        sourceTime: "2024-01-15T11:45:00Z",
      },
    },
    name: "Conference Room A",
    roomNumber: "101A",
    area: 45,
    capacity: 12,
    temperature: 22.5,
    humidity: 45,
    occupancy: 8,
  };

  const { $dtId, $metadata, ...properties } = mockTwin;
  const modelDisplayName = getModelDisplayName($metadata.$model);

  return (
    <div className="space-y-4">
      {/* Twin Identity */}
      <div className="space-y-2">
        <h3 className="font-semibold text-sm flex items-center gap-2">
          <Database className="w-4 h-4" />
          Digital Twin
        </h3>
        <div className="space-y-2">
          <div className="flex justify-between items-start text-sm">
            <span className="text-muted-foreground">Twin ID</span>
            <code className="font-mono text-xs bg-muted px-2 py-1 rounded">
              {$dtId}
            </code>
          </div>
          <div className="flex justify-between items-center text-sm">
            <span className="text-muted-foreground">Model</span>
            <div className="flex items-center gap-2">
              <Badge variant="outline" className="text-xs">
                {modelDisplayName}
              </Badge>
            </div>
          </div>
          <div className="flex justify-between items-start text-sm">
            <span className="text-muted-foreground">Model ID</span>
            <code className="font-mono text-xs text-right break-all max-w-[200px]">
              {$metadata.$model}
            </code>
          </div>
        </div>
      </div>

      {/* Properties */}
      <div className="space-y-2">
        <h3 className="font-semibold text-sm flex items-center gap-2">
          <FileText className="w-4 h-4" />
          Properties
        </h3>
        <div className="space-y-2">
          {Object.entries(properties).map(([key, value]) => (
            <div key={key} className="flex justify-between items-start text-sm">
              <span className="text-muted-foreground min-w-0 flex-1">
                {key}
              </span>
              <div className="ml-2 text-right">
                <div className="font-mono text-xs break-all">
                  {typeof value === "object" && value !== null
                    ? JSON.stringify(value)
                    : String(value)}
                </div>
                {/* Show metadata if available */}
                {(() => {
                  const meta = $metadata[key];
                  if (meta && typeof meta === "object" && "lastUpdateTime" in meta) {
                    const dtMeta = meta as import("@/types/BasicDigitalTwin").DigitalTwinPropertyMetadata;
                    return dtMeta.lastUpdateTime ? (
                      <div className="text-xs text-muted-foreground mt-1 flex items-center gap-1">
                        <Clock className="w-3 h-3" />
                        {new Date(dtMeta.lastUpdateTime).toLocaleString()}
                      </div>
                    ) : null;
                  }
                  return null;
                })()}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Metadata */}
      <div className="space-y-2">
        <h3 className="font-semibold text-sm flex items-center gap-2">
          <Zap className="w-4 h-4" />
          Metadata
        </h3>
        <div className="space-y-2">
          {Object.entries($metadata)
            .filter(
              ([key]) =>
                !key.startsWith("$") && typeof $metadata[key] === "object"
            )
            .map(([key, metadata]) => (
              <div key={key} className="border rounded-md p-2 text-sm">
                <div className="font-medium mb-1">{key}</div>
                <div className="space-y-1 text-xs text-muted-foreground">
                  {(() => {
                    if (typeof metadata === "object" && metadata && "lastUpdateTime" in metadata) {
                      const dtMeta = metadata as import("@/types/BasicDigitalTwin").DigitalTwinPropertyMetadata;
                      return <>
                        {dtMeta.lastUpdateTime && (
                          <div className="flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            Updated:{" "}
                            {new Date(dtMeta.lastUpdateTime).toLocaleString()}
                          </div>
                        )}
                        {dtMeta.sourceTime && (
                          <div className="flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            Source:{" "}
                            {new Date(dtMeta.sourceTime).toLocaleString()}
                          </div>
                        )}
                        {dtMeta.desiredValue !== undefined && (
                          <div>
                            Desired: {String(dtMeta.desiredValue)}
                          </div>
                        )}
                      </>;
                    }
                    return null;
                  })()}
                </div>
              </div>
            ))}
        </div>
      </div>

      {/* Quick Actions */}
      <div className="pt-4 border-t">
        <div className="text-xs text-muted-foreground space-y-1">
          <div>Select twin in query results to inspect</div>
          <div>Real-time updates: Coming soon</div>
        </div>
      </div>
    </div>
  );
}
