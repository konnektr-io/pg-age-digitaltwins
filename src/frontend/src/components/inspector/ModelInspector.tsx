import { FileCode2, Layers, Tag, Clock, Info } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { mockModels } from "@/mocks/digitalTwinData";

interface ModelInspectorProps {
  modelId: string;


export function ModelInspector({ modelId }: ModelInspectorProps) {
  // Find the model in our mock data
  const modelData = mockModels.find((model) => {
    if (
      typeof model.model === "object" &&
      model.model !== null &&
      "@id" in model.model
    ) {
      const idVal = (model.model as Record<string, unknown>)["@id"];
      return typeof idVal === "string" && idVal === modelId;
    }
    return false;
  });

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
              {typeof model === "object" &&
              model !== null &&
              typeof (model as Record<string, unknown>)["@id"] === "string"
                ? String((model as Record<string, unknown>)["@id"])
                : ""}
            </code>
          </div>
          <div className="flex justify-between items-center text-sm">
			<span className="text-muted-foreground">Upload Time</span>
          </div>
          <div className="flex justify-between items-start text-sm">
            <span className="text-muted-foreground">Display Name</span>
            <span className="text-right break-all max-w-[200px]">
              {typeof model === "object" &&
              model !== null &&
              (model as Record<string, unknown>)["displayName"] !== undefined
                ? String((model as Record<string, unknown>)["displayName"])
                : "No display name"}
            </span>
          </div>
          {typeof model === "object" &&
            model !== null &&
            (model as Record<string, unknown>)["description"] !== undefined && (
              <div className="flex justify-between items-start text-sm">
                <span className="text-muted-foreground">Description</span>
                <span className="text-right text-xs max-w-[200px] break-words">
                  {(() => {
                    const desc = (model as Record<string, unknown>)["description"];
                    if (typeof desc === "string") return desc;
                    if (desc && typeof desc === "object" && "en" in desc) return String((desc as Record<string, unknown>)["en"]);
                    return JSON.stringify(desc);
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
          <code className="font-mono text-xs bg-muted px-2 py-1 rounded">{id}</code>
        </div>
      </div>

      {/* Contents/Properties */}
      {typeof model === "object" &&
        model !== null &&
        Array.isArray((model as Record<string, unknown>)["contents"]) &&
        model.contents && model.contents.length > 0 && (
          <div className="space-y-2">
            <h3 className="font-semibold text-sm flex items-center gap-2">
              <Layers className="w-4 h-4" />
              Contents (
              {((model as Record<string, unknown>)["contents"] as unknown[]).length}
              )
            </h3>
            <div className="space-y-2 max-h-48 overflow-y-auto">
              {model.contents?.map((content, index) => {
                if (typeof content !== "object" || content === null) return null;
                const c = content;
                return (
                  <div key={index} className="border rounded-md p-2 text-sm">
                    <div className="flex justify-between items-start mb-1">
                      <span className="font-medium">
                        {typeof c.name === "string" ? c.name : String(c.name ?? "")}
                      </span>
                      <Badge variant="secondary" className="text-xs">
                        {typeof c["@type"] === "string" ? c["@type"] : String(c["@type"] ?? "")}
                      </Badge>
                    </div>
                    {c.displayName !== undefined && (
                      <div className="text-xs text-muted-foreground mb-1">
                        {(() => {
                          const dn = c.displayName;
                          if (typeof dn === "string") return dn;
                          if (dn && typeof dn === "object" && "en" in dn) return String((dn as Record<string, unknown>)["en"]);
                          return JSON.stringify(dn);
                        })()}
                      </div>
                    )}
                    {c.description !== undefined && (
                      <div className="text-xs text-muted-foreground break-words">
                        {(() => {
                          const desc = c.description;
                          if (typeof desc === "string") return desc;
                          if (desc && typeof desc === "object" && "en" in desc) return String((desc as Record<string, unknown>)["en"]);
                          return JSON.stringify(desc);
                        })()}
                      </div>
                    )}
                    {c['@type'] === 'Property' && c.schema !== undefined && (
                      <div className="text-xs mt-1">
                        <span className="text-muted-foreground">Schema: </span>
                        <code className="font-mono">
                          {typeof c.schema === "string" ? c.schema : JSON.stringify(c.schema)}
                        </code>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}

      {/* Extends */}
      {typeof model === "object" &&
        model !== null &&
        (model as Record<string, unknown>)["extends"] && (
          <div className="space-y-2">
            <h3 className="font-semibold text-sm flex items-center gap-2">
              <Tag className="w-4 h-4" />
              Extends
            </h3>
            <div className="space-y-1">
              {(Array.isArray((model as Record<string, unknown>)["extends"])
                ? ((model as Record<string, unknown>)["extends"] as unknown[])
                : [(model as Record<string, unknown>)["extends"]]
              ).map((extend, index) => (
                <code
                  key={index}
                  className="block font-mono text-xs bg-muted px-2 py-1 rounded break-all"
                >
                  {typeof extend === "string" ? extend : String(extend)}
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
          <div>
            Model validation:{" "}
            {typeof model === "object" &&
            model !== null &&
            (model as Record<string, unknown>)["contents"]
              ? "Valid"
              : "Unknown"}
          </div>
        </div>
      </div>
    </div>
  );
}
