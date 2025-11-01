import React from "react";
import { GitBranch, Database, Tag } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import type { BasicRelationship } from "@/types";

interface RelationshipInspectorProps {
  relationshipId: string;
}

export function RelationshipInspector({
  relationshipId,
}: RelationshipInspectorProps): React.JSX.Element {
  // In a real app, this would fetch the relationship data from the API
  // For now, we'll simulate loading the relationship data
  const relationship: BasicRelationship = {
    $relationshipId: relationshipId,
    $sourceId: "Room_101A",
    $targetId: "Sensor_T_101A_1",
    $relationshipName: "contains",
    installedDate: "2024-01-10",
    maintenanceSchedule: "monthly",
    status: "active",
  };

  return (
    <div className="space-y-4">
      {/* Relationship Identity */}
      <div className="space-y-2">
        <h3 className="font-semibold text-sm flex items-center gap-2">
          <GitBranch className="w-4 h-4" />
          Relationship
        </h3>
        <div className="space-y-2">
          <div className="flex justify-between items-start text-sm">
            <span className="text-muted-foreground">Relationship ID</span>
            <code className="font-mono text-xs bg-muted px-2 py-1 rounded">
              {relationship.$relationshipId}
            </code>
          </div>
          <div className="flex justify-between items-center text-sm">
            <span className="text-muted-foreground">Type</span>
            <Badge variant="secondary" className="text-xs">
              {relationship.$relationshipName}
            </Badge>
          </div>
        </div>
      </div>

      {/* Connection Details */}
      <div className="space-y-2">
        <h3 className="font-semibold text-sm flex items-center gap-2">
          <Database className="w-4 h-4" />
          Connection
        </h3>
        <div className="space-y-3">
          <div className="flex items-center justify-between p-2 border rounded-md">
            <div className="text-sm">
              <div className="font-medium">Source Twin</div>
              <div className="text-xs text-muted-foreground">
                {relationship.$sourceId}
              </div>
            </div>
            <div className="mx-2 text-muted-foreground">→</div>
            <div className="text-sm text-right">
              <div className="font-medium">Target Twin</div>
              <div className="text-xs text-muted-foreground">
                {relationship.$targetId}
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Properties */}
      {Object.keys(relationship).filter((k) => !k.startsWith("$")).length >
        0 && (
        <div className="space-y-2">
          <h3 className="font-semibold text-sm flex items-center gap-2">
            <Tag className="w-4 h-4" />
            Properties
          </h3>
          <div className="space-y-2">
            {Object.entries(relationship)
              .filter(([key]) => !key.startsWith("$"))
              .map(([key, value]) => (
                <div
                  key={key}
                  className="flex justify-between items-start text-sm"
                >
                  <span className="text-muted-foreground min-w-0 flex-1">
                    {key}
                  </span>
                  <div className="ml-2 text-right">
                    <div className="font-mono text-xs break-all">
                      {typeof value === "object" && value !== null
                        ? JSON.stringify(value)
                        : String(value ?? "")}
                    </div>
                  </div>
                </div>
              ))}
          </div>
        </div>
      )}

      {/* Quick Actions */}
      <div className="pt-4 border-t">
        <div className="text-xs text-muted-foreground space-y-1">
          <div>Select relationship in query results to inspect</div>
          <div>Navigate to connected twins: Coming soon</div>
        </div>
      </div>
    </div>
  );
}
