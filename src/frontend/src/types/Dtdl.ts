import type { DtdlLocalizableString } from './DtdlLocalizableString'

export type DtdlPrimitiveSchema =
  | 'boolean'
  | 'date'
  | 'dateTime'
  | 'double'
  | 'duration'
  | 'float'
  | 'integer'
  | 'long'
  | 'string'
  | 'time'

export interface DtdlEnumValue {
  name: string
  enumValue: string | number
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  comment?: string
  '@id'?: string
}

export interface DtdlEnumSchema {
  '@id'?: string
  '@type': 'Enum'
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  enumValues: DtdlEnumValue[]
  valueSchema: 'integer' | 'string'
}

export interface DtdlMapSchema {
  '@id'?: string
  '@type': 'Map'
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  mapKey: {
    '@id'?: string
    name: string
    schema: 'string'
    displayName?: DtdlLocalizableString
    description?: DtdlLocalizableString
    comment?: string
  }
  mapValue: {
    '@id'?: string
    name: string
    comment?: string
    displayName?: DtdlLocalizableString
    description?: DtdlLocalizableString
    schema: DtdlSchema
  }
}

export interface DtdlObjectField {
  '@id'?: string
  '@type'?: 'Field' | (string[] & { 0: 'Field' })
  name: string
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  schema: DtdlSchema
}

export interface DtdlObjectSchema {
  '@id'?: string
  '@type': 'Object' | (string[] & { 0: 'Object' })
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  fields: DtdlObjectField[]
}

export interface DtdlArraySchema {
  '@id'?: string
  '@type': 'Array'
  elementSchema: DtdlSchema
}

export type DtdlComplexSchema = DtdlEnumSchema | DtdlMapSchema | DtdlObjectSchema | DtdlArraySchema
export type DtdlSchema = DtdlComplexSchema | DtdlPrimitiveSchema

export interface InvalidProperty {
  invalid: boolean
  name: string
}
