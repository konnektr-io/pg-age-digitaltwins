import { FileCode2, Layers, Tag, Info } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useModelsStore } from "@/stores/modelsStore";

interface ModelInspectorProps {
  modelId: string;
}

export function ModelInspector({ modelId }: ModelInspectorProps) {
  const { models } = useModelsStore();

  // Find the model by ID
  const modelData = models.find((m) => m.id === modelId);

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

  const { model, id } = modelData;

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
              {model?.["@id"] || ""}
            </code>
          </div>
          <div className="flex justify-between items-center text-sm">
            <span className="text-muted-foreground">Upload Time</span>
          </div>
          <div className="flex justify-between items-start text-sm">
            <span className="text-muted-foreground">Display Name</span>
            <span className="text-right break-all max-w-[200px]">
              {(() => {
                if (!model?.displayName) return "No display name";
                if (typeof model.displayName === "string")
                  return model.displayName;
                if (
                  typeof model.displayName === "object" &&
                  "en" in model.displayName
                ) {
                  return String(model.displayName.en);
                }
                return JSON.stringify(model.displayName);
              })()}
            </span>
          </div>
          {model?.description && (
            <div className="flex justify-between items-start text-sm">
              <span className="text-muted-foreground">Description</span>
              <span className="text-right text-xs max-w-[200px] break-words">
                {(() => {
                  if (typeof model?.description === "string")
                    return model.description;
                  if (
                    model?.description &&
                    typeof model.description === "object" &&
                    "en" in model.description
                  ) {
                    return String(
                      (model.description as Record<string, unknown>)["en"]
                    );
                  }
                  return model?.description
                    ? JSON.stringify(model.description)
                    : "";
                })()}
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
        <div className="flex justify-between items-start text-sm">
          <span className="text-muted-foreground">Model ID</span>
          <code className="font-mono text-xs bg-muted px-2 py-1 rounded">
            {id}
          </code>
        </div>
      </div>

      {/* Contents/Properties */}
      {model?.contents && model.contents.length > 0 && (
        <div className="space-y-2">
          <h3 className="font-semibold text-sm flex items-center gap-2">
            <Layers className="w-4 h-4" />
            Contents ({model.contents.length})
          </h3>
          <div className="space-y-2 max-h-48 overflow-y-auto">
            {model.contents.map((content, index) => (
              <div key={index} className="border rounded-md p-2 text-sm">
                <div className="flex justify-between items-start mb-1">
                  <span className="font-medium">{content.name || ""}</span>
                  <Badge variant="secondary" className="text-xs">
                    {Array.isArray(content["@type"])
                      ? content["@type"][0]
                      : content["@type"]}
                  </Badge>
                </div>
                {content.displayName && (
                  <div className="text-xs text-muted-foreground mb-1">
                    {typeof content.displayName === "string"
                      ? content.displayName
                      : content.displayName.en ||
                        JSON.stringify(content.displayName)}
                  </div>
                )}
                {content.description && (
                  <div className="text-xs text-muted-foreground break-words">
                    {typeof content.description === "string"
                      ? content.description
                      : content.description.en ||
                        JSON.stringify(content.description)}
                  </div>
                )}
                {"schema" in content && content.schema && (
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
      {model?.extends && (
        <div className="space-y-2">
          <h3 className="font-semibold text-sm flex items-center gap-2">
            <Tag className="w-4 h-4" />
            Extends
          </h3>
          <div className="space-y-1">
            {(Array.isArray(model.extends)
              ? model.extends
              : [model.extends]
            ).map((extend, index) => (
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
          <div>Model validation: {model?.contents ? "Valid" : "Unknown"}</div>
        </div>
      </div>
    </div>
  );
}
