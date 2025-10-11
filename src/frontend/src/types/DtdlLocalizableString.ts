export interface DtdlLocalizedString {
  [countryCode: string]: string
}

export type DtdlLocalizableString = string | DtdlLocalizedString
