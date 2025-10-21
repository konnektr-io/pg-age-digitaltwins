/**
 * Shared helper functions for table view components
 */

/**
 * Get all columns containing entity objects (with $dtId)
 */
export function getEntityColumns(results: unknown[]): string[] {
  if (results.length === 0) return [];
  const firstRow = results[0];
  if (typeof firstRow !== "object" || firstRow === null) return [];
  return Object.keys(firstRow).filter((key) => {
    const value = (firstRow as Record<string, unknown>)[key];
    return typeof value === "object" && value !== null && "$dtId" in value;
  });
}

/**
 * Extract properties from an entity (excluding $metadata)
 */
export function getEntityProperties(entity: unknown): [string, unknown][] {
  if (typeof entity !== "object" || entity === null) return [];
  const entries = Object.entries(entity);
  return entries.filter(([key]) => key !== "$metadata");
}

/**
 * Get entity type from metadata model
 */
export function getEntityType(entity: unknown): string {
  if (
    typeof entity === "object" &&
    entity !== null &&
    "$metadata" in entity &&
    typeof entity.$metadata === "object" &&
    entity.$metadata !== null &&
    "$model" in entity.$metadata
  ) {
    const model = String(entity.$metadata.$model);
    const parts = model.split(":");
    return parts[parts.length - 1]?.split(";")[0] || "Entity";
  }
  return "Entity";
}
