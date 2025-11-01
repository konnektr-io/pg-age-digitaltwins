// Re-export all Digital Twin interfaces for easy importing
export type {
  BasicDigitalTwin,
  BasicDigitalTwinComponent,
  DigitalTwinPropertyMetadata,
} from "./BasicDigitalTwin";
export type { BasicRelationship } from "./BasicRelationship";
export type { DigitalTwinsModelData } from "./DigitalTwinsModelData";
export type { DigitalTwinsModelDataExtended } from "./DigitalTwinsModelDataExtended";
export type { DtdlInterface, DtdlContent } from "./DtdlInterface";
export type {
  DtdlProperty,
  DtdlObjectProperty,
  DtdlMapProperty,
} from "./DtdlProperty";
export type { DtdlRelationship } from "./DtdlRelationship";
export type { DtdlTelemetry } from "./DtdlTelemetry";
export type { DtdlComponent } from "./DtdlComponent";
export type {
  DtdlLocalizableString,
  DtdlLocalizedString,
} from "./DtdlLocalizableString";
export type {
  DtdlSchema,
  DtdlPrimitiveSchema,
  DtdlComplexSchema,
  DtdlEnumSchema,
  DtdlMapSchema,
  DtdlObjectSchema,
  DtdlArraySchema,
  DtdlEnumValue,
  DtdlObjectField,
} from "./Dtdl";
export type { SimpleProperty } from "./DtdlSimpleProperty";
