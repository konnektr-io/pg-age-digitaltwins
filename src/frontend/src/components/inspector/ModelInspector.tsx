import { FileCode2, Layers, Tag, Clock, Info } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { mockModels } from "@/mocks/digitalTwinData";

interface ModelInspectorProps {
  modelId: string;
}

export function ModelInspector({ modelId }: ModelInspectorProps) {
  // Find the model in our mock data
  const modelData = mockModels.find(
    (model) => (model.model as any)["@id"] === modelId
  );

  if (!modelData) {
    return (
      <div className="space-y-4">
        <div className="text-center py-8 text-muted-foreground">
          <FileCode2 className="w-8 h-8 mx-auto mb-2" />
          <div>Model not found</div>
          <div className="text-sm">Model ID: {modelId}</div>
        </div>
      </div>
    );
  }

  const { model: dtdlModel, uploadTime } = modelData;
  const model = dtdlModel as any;

  return (
    <div className="space-y-4">
      {/* Model Identity */}
      <div className="space-y-2">
        <h3 className="font-semibold text-sm flex items-center gap-2">
          <FileCode2 className="w-4 h-4" />
          Model Definition
        </h3>
        <div className="space-y-2">
          <div className="flex justify-between items-start text-sm">
            <span className="text-muted-foreground">Model ID</span>
            <code className="font-mono text-xs bg-muted px-2 py-1 rounded break-all max-w-[200px] text-right">
              {model["@id"]}
            </code>
          </div>
          <div className="flex justify-between items-center text-sm">
            <span className="text-muted-foreground">Type</span>
            <Badge variant="outline" className="text-xs">
              {model["@type"] || "Interface"}
            </Badge>
          </div>
          <div className="flex justify-between items-start text-sm">
            <span className="text-muted-foreground">Display Name</span>
            <span className="text-right break-all max-w-[200px]">
              {model.displayName || "No display name"}
            </span>
          </div>
          {model.description && (
            <div className="flex justify-between items-start text-sm">
              <span className="text-muted-foreground">Description</span>
              <span className="text-right text-xs max-w-[200px] break-words">
                {model.description}
              </span>
            </div>
          )}
        </div>
      </div>

      {/* Context & Version */}
      <div className="space-y-2">
        <h3 className="font-semibold text-sm flex items-center gap-2">
          <Info className="w-4 h-4" />
          Context & Version
        </h3>
        <div className="space-y-2">
          {model["@context"] && (
            <div className="flex justify-between items-start text-sm">
              <span className="text-muted-foreground">Context</span>
              <code className="text-xs text-right break-all max-w-[200px]">
                {Array.isArray(model["@context"])
                  ? model["@context"].join(", ")
                  : model["@context"]}
              </code>
            </div>
          )}
          <div className="flex justify-between items-start text-sm">
            <span className="text-muted-foreground">Model ID</span>
            <code className="font-mono text-xs bg-muted px-2 py-1 rounded">
              {String(modelData.id)}
            </code>
          </div>
          <div className="flex justify-between items-center text-sm">
            <span className="text-muted-foreground">Upload Time</span>
            <div className="flex items-center gap-1 text-xs text-muted-foreground">
              <Clock className="w-3 h-3" />
              {(uploadTime as Date).toLocaleString()}
            </div>
          </div>
        </div>
      </div>

      {/* Contents/Properties */}
      {model.contents && model.contents.length > 0 && (
        <div className="space-y-2">
          <h3 className="font-semibold text-sm flex items-center gap-2">
            <Layers className="w-4 h-4" />
            Contents ({model.contents.length})
          </h3>
          <div className="space-y-2 max-h-48 overflow-y-auto">
            {model.contents.map((content: any, index: number) => (
              <div key={index} className="border rounded-md p-2 text-sm">
                <div className="flex justify-between items-start mb-1">
                  <span className="font-medium">{content.name}</span>
                  <Badge variant="secondary" className="text-xs">
                    {content["@type"]}
                  </Badge>
                </div>
                {content.displayName && (
                  <div className="text-xs text-muted-foreground mb-1">
                    {content.displayName}
                  </div>
                )}
                {content.description && (
                  <div className="text-xs text-muted-foreground break-words">
                    {content.description}
                  </div>
                )}
                {content.schema && (
                  <div className="text-xs mt-1">
                    <span className="text-muted-foreground">Schema: </span>
                    <code className="font-mono">
                      {typeof content.schema === "string"
                        ? content.schema
                        : JSON.stringify(content.schema)}
                    </code>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Extends */}
      {model.extends && (
        <div className="space-y-2">
          <h3 className="font-semibold text-sm flex items-center gap-2">
            <Tag className="w-4 h-4" />
            Extends
          </h3>
          <div className="space-y-1">
            {(Array.isArray(model.extends)
              ? model.extends
              : [model.extends]
            ).map((extend: any, index: number) => (
              <code
                key={index}
                className="block font-mono text-xs bg-muted px-2 py-1 rounded break-all"
              >
                {extend}
              </code>
            ))}
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="pt-4 border-t space-y-2">
        <Button variant="outline" size="sm" className="w-full text-xs">
          View Full DTDL Definition
        </Button>
        <div className="text-xs text-muted-foreground space-y-1">
          <div>Select model in sidebar or query results</div>
          <div>Model validation: {model.contents ? "Valid" : "Unknown"}</div>
        </div>
      </div>
    </div>
  );
}
