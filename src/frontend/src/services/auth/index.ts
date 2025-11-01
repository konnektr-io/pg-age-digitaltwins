/**
 * Authentication services for Graph Explorer
 *
 * Exports token credential implementations for different auth providers:
 * - MSAL: Azure Digital Twins with PKCE flow
 * - Auth0: Konnektr hosted instances
 */

export {
  MsalTokenCredential,
  Auth0TokenCredential,
  useAuth0Credential,
  getTokenCredential,
} from "./tokenCredentialFactory";
