import type { DtdlLocalizableString } from './DtdlLocalizableString'

export interface DtdlComponent {
  '@id'?: string
  /** Type should be 'Telemetry', with or without semantic type. Optional to allow using it as field in object */
  '@type': 'Component'
  name: string
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  /** Component schema is always a dtmi */
  schema: string
}
