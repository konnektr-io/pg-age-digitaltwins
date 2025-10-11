import type { DtdlLocalizableString } from "./DtdlLocalizableString";

export interface SimpleProperty {
  ["@id"]?: string;
  ["@type"]?: string | string[];
  name?: string;
  displayName?: DtdlLocalizableString;
  description?: DtdlLocalizableString;
  comment?: string;
  schema?: string | { ["@type"]: string | string[] };
}
