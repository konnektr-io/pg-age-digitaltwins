import React, { useState, useEffect } from "react";
import { Check, X, Edit2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { MetadataTooltip } from "@/components/ui/metadata-tooltip";
import type { DigitalTwinPropertyMetadata } from "@/types";

interface EditablePropertyProps {
  name: string;
  value: unknown;
  metadata?: DigitalTwinPropertyMetadata;
  type?: "string" | "number" | "boolean" | "auto";
  onSave: (newValue: unknown) => Promise<void>;
  className?: string;
}

export function EditableProperty({
  name,
  value,
  metadata,
  type = "auto",
  onSave,
  className,
}: EditablePropertyProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editValue, setEditValue] = useState<string>("");
  const [isSaving, setIsSaving] = useState(false);

  // Determine the property type
  const propertyType = type === "auto" ? typeof value : type;

  useEffect(() => {
    if (isEditing) {
      setEditValue(String(value ?? ""));
    }
  }, [isEditing, value]);

  const handleSave = async () => {
    try {
      setIsSaving(true);
      let parsedValue: unknown = editValue;

      // Parse value based on type
      if (propertyType === "number") {
        const num = parseFloat(editValue);
        if (isNaN(num)) {
          throw new Error("Invalid number");
        }
        parsedValue = num;
      } else if (propertyType === "boolean") {
        parsedValue = editValue.toLowerCase() === "true" || editValue === "1";
      }

      await onSave(parsedValue);
      setIsEditing(false);
    } catch (error) {
      console.error("Failed to save property:", error);
      // TODO: Show error toast
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    setIsEditing(false);
    setEditValue(String(value ?? ""));
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSave();
    } else if (e.key === "Escape") {
      e.preventDefault();
      handleCancel();
    }
  };

  // Format display value
  const formatValue = (val: unknown): string => {
    if (val === null || val === undefined) return "";
    if (propertyType === "boolean") return val ? "true" : "false";
    if (propertyType === "number") return String(val);
    return String(val);
  };

  return (
    <div className={`flex items-start text-sm gap-3 ${className || ""}`}>
      <span className="text-muted-foreground whitespace-nowrap flex-shrink-0 min-w-0">
        {name}
      </span>

      {isEditing ? (
        <div className="flex items-center gap-2 flex-1 min-w-0">
          {propertyType === "boolean" ? (
            <Switch
              checked={editValue === "true"}
              onCheckedChange={(checked: boolean) =>
                setEditValue(checked ? "true" : "false")
              }
              disabled={isSaving}
            />
          ) : (
            <Input
              value={editValue}
              onChange={(e) => setEditValue(e.target.value)}
              onKeyDown={handleKeyDown}
              className="h-8 text-xs"
              type={propertyType === "number" ? "number" : "text"}
              disabled={isSaving}
              autoFocus
            />
          )}

          <div className="flex items-center gap-1">
            <Button
              size="sm"
              variant="ghost"
              className="h-6 w-6 p-0"
              onClick={handleSave}
              disabled={isSaving}
            >
              <Check className="w-3 h-3" />
            </Button>
            <Button
              size="sm"
              variant="ghost"
              className="h-6 w-6 p-0"
              onClick={handleCancel}
              disabled={isSaving}
            >
              <X className="w-3 h-3" />
            </Button>
          </div>
        </div>
      ) : (
        <MetadataTooltip metadata={metadata} className="flex-1 min-w-0">
          <div
            className="text-right cursor-pointer hover:bg-muted/50 px-2 py-1 rounded group w-full"
            onClick={() => setIsEditing(true)}
          >
            <div className="flex items-center gap-1.5 justify-end">
              <div className="flex items-center gap-0 font-medium text-right">
                <span className="break-words">{formatValue(value)}</span>
                {propertyType === "number" &&
                  typeof value === "number" &&
                  (name.toLowerCase().includes("temp") ? (
                    <span className="whitespace-nowrap">°C</span>
                  ) : name.toLowerCase().includes("area") ? (
                    <span className="whitespace-nowrap">m²</span>
                  ) : (
                    ""
                  ))}
              </div>
              <Edit2 className="w-3 h-3 opacity-0 group-hover:opacity-50 transition-opacity" />
            </div>
          </div>
        </MetadataTooltip>
      )}
    </div>
  );
}
