import { FileText, Database } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { EditableProperty } from "./EditableProperty";
import { MetadataTooltip } from "@/components/ui/metadata-tooltip";
import { useDigitalTwinsStore } from "@/stores/digitalTwinsStore";
import type { BasicDigitalTwin, DigitalTwinPropertyMetadata } from "@/types";
import { getModelDisplayName } from "@/utils/dtdlHelpers";

interface TwinInspectorProps {
  twinId: string;
}

export function TwinInspector({ twinId }: TwinInspectorProps) {
  const { getTwin, updateTwinProperty } = useDigitalTwinsStore();

  // In a real app, this would come from the store
  // For now, simulate loading the twin data
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

  const twin = getTwin(twinId) || mockTwin;
  const { $dtId, $metadata, ...properties } = twin;
  const modelDisplayName = getModelDisplayName($metadata.$model);

  const handlePropertySave = async (
    propertyName: string,
    newValue: unknown
  ) => {
    await updateTwinProperty(twinId, propertyName, newValue);
  };

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
          {Object.entries(properties).map(([key, value]) => {
            const metadata = $metadata[key] as
              | DigitalTwinPropertyMetadata
              | undefined;
            return (
              <EditableProperty
                key={key}
                name={key}
                value={value}
                metadata={metadata}
                onSave={(newValue) => handlePropertySave(key, newValue)}
              />
            );
          })}
        </div>
      </div>

      {/* Quick Actions */}
      <div className="pt-4 border-t">
        <MetadataTooltip
          metadata={{
            lastUpdateTime: "2024-01-15T10:32:00Z",
          }}
        >
          <div className="text-xs text-muted-foreground space-y-1">
            <div>Click properties to edit • Hover for metadata</div>
            <div>Changes save automatically</div>
          </div>
        </MetadataTooltip>
      </div>
    </div>
  );
}
