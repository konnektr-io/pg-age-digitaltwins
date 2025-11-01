import type { DtdlSchema } from './Dtdl'
import type { DtdlLocalizableString } from './DtdlLocalizableString'

export interface DtdlTelemetry {
  '@id'?: string
  /** Type should be 'Telemetry', with or without semantic type. Optional to allow using it as field in object */
  '@type': 'Telemetry' | (string[] & { 0: 'Telemetry' })
  name: string
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  schema: DtdlSchema // Can also be an Array, but not taken into account as this isn't being used (yet)
  unit?: string
}
