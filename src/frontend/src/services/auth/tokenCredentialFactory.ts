import type { TokenCredential } from "@azure/core-auth";
import {
  PublicClientApplication,
  InteractionRequiredAuthError,
} from "@azure/msal-browser";
import { useAuth0 } from "@auth0/auth0-react";
import type { Connection, AuthConfig } from "@/stores/connectionStore";

/**
 * MSAL Token Credential implementation for Azure Digital Twins
 * Uses PKCE flow with browser redirect authentication
 */
export class MsalTokenCredential implements TokenCredential {
  private msalInstance: PublicClientApplication;
  private scopes: string[];
  private isInitialized = false;
  private isRedirecting = false;

  constructor(config: AuthConfig) {
    if (!config.clientId || !config.tenantId) {
      throw new Error("MSAL requires clientId and tenantId");
    }

    // Default scopes for Azure Digital Twins
    this.scopes = config.scopes || ["https://digitaltwins.azure.net/.default"];

    // Initialize MSAL with PKCE configuration
    this.msalInstance = new PublicClientApplication({
      auth: {
        clientId: config.clientId,
        authority: `https://login.microsoftonline.com/${config.tenantId}`,
        redirectUri: config.redirectUri || window.location.origin,
      },
      cache: {
        cacheLocation: "localStorage",
        storeAuthStateInCookie: false,
      },
    });
  }

  /**
   * Initialize MSAL instance (must be called before getToken)
   */
  async initialize(): Promise<void> {
    if (this.isInitialized) {
      return; // Already initialized
    }

    await this.msalInstance.initialize();

    // Handle redirect promise on page load
    const resp = await this.msalInstance.handleRedirectPromise();
    console.log("MSAL redirect response:", resp);

    this.isInitialized = true;
  }

  /**
   * Get access token for Azure Digital Twins API
   * Implements TokenCredential interface from @azure/core-auth
   */
  async getToken(
    _scopes: string | string[]
  ): Promise<{ token: string; expiresOnTimestamp: number }> {
    // Ensure MSAL is initialized
    if (!this.isInitialized) {
      await this.initialize();
    }

    const accounts = this.msalInstance.getAllAccounts();

    if (accounts.length === 0) {
      // No user signed in, trigger interactive login
      if (this.isRedirecting) {
        // Already redirecting, don't trigger another redirect
        throw new Error("Authentication in progress - redirecting to sign in");
      }

      this.isRedirecting = true;
      console.log("No MSAL accounts found, triggering login redirect...");

      try {
        await this.msalInstance.loginRedirect({
          scopes: this.scopes,
        });
      } catch (error) {
        this.isRedirecting = false;
        throw error;
      }

      // Redirect happens, token will be acquired after redirect
      throw new Error("Redirecting to sign in...");
    }

    const account = accounts[0];
    console.log("Using MSAL account:", account.username);

    try {
      // Try silent token acquisition first
      const result = await this.msalInstance.acquireTokenSilent({
        scopes: this.scopes,
        account,
      });

      console.log(
        "Token acquired successfully, expires:",
        new Date(result.expiresOn || 0)
      );

      return {
        token: result.accessToken,
        expiresOnTimestamp: result.expiresOn?.getTime() || Date.now() + 3600000,
      };
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) {
        console.warn("Interaction required for token refresh:", error);

        // Check if we're already redirecting to avoid loops
        if (this.isRedirecting) {
          throw new Error(
            "Authentication in progress - token refresh required"
          );
        }

        this.isRedirecting = true;

        try {
          // Silent acquisition failed, trigger interactive
          await this.msalInstance.acquireTokenRedirect({
            scopes: this.scopes,
            account,
          });
        } catch (redirectError) {
          this.isRedirecting = false;
          throw redirectError;
        }

        // Note: acquireTokenRedirect doesn't return a result, it redirects
        // Token will be acquired after redirect
        throw new Error("Redirecting for token refresh...");
      }

      console.error("Token acquisition failed:", error);
      throw error;
    }
  }

  /**
   * Sign out the user
   */
  async logout(): Promise<void> {
    const accounts = this.msalInstance.getAllAccounts();
    if (accounts.length > 0) {
      await this.msalInstance.logoutRedirect({
        account: accounts[0],
      });
    }
  }
}

/**
 * Auth0 Token Credential implementation for Konnektr hosted instances
 *
 * Note: This credential requires the Auth0Provider context to be available.
 * It cannot be instantiated directly - use getAuth0Credential() hook instead.
 */
export class Auth0TokenCredential implements TokenCredential {
  private getAccessTokenSilently: () => Promise<string>;

  constructor(
    getAccessTokenSilently: () => Promise<string>,
    _audience: string
  ) {
    this.getAccessTokenSilently = getAccessTokenSilently;
  }

  /**
   * Get access token from Auth0
   * Implements TokenCredential interface from @azure/core-auth
   */
  async getToken(): Promise<{ token: string; expiresOnTimestamp: number }> {
    try {
      const token = await this.getAccessTokenSilently();

      return {
        token,
        // Auth0 tokens typically expire in 1 hour
        expiresOnTimestamp: Date.now() + 3600000,
      };
    } catch (error) {
      console.error("Failed to get Auth0 token:", error);
      throw new Error("Authentication required. Please sign in.");
    }
  }
}

/**
 * React hook to create Auth0 credential from Auth0 context
 * Must be used within Auth0Provider
 */
export function useAuth0Credential(
  config: AuthConfig
): Auth0TokenCredential | null {
  const { getAccessTokenSilently, isAuthenticated } = useAuth0();

  if (!isAuthenticated || !config.audience) {
    return null;
  }

  return new Auth0TokenCredential(getAccessTokenSilently, config.audience);
}

/**
 * Factory function to create appropriate token credential based on connection config
 *
 * @param connection - Connection with auth provider and config
 * @returns TokenCredential instance
 *
 * Note: For Auth0, you must use useAuth0Credential() hook instead, as it requires
 * Auth0Provider context. This factory only supports MSAL.
 */
export async function getTokenCredential(
  connection: Connection
): Promise<TokenCredential | null> {
  const { authProvider, authConfig } = connection;

  if (authProvider === "none") {
    return null; // No authentication required
  }

  if (authProvider === "msal") {
    if (!authConfig) {
      throw new Error("MSAL authentication requires configuration");
    }
    const credential = new MsalTokenCredential(authConfig);
    await credential.initialize();
    return credential;
  }

  if (authProvider === "auth0") {
    throw new Error(
      "Auth0 credentials must be created using useAuth0Credential() hook within Auth0Provider context"
    );
  }

  throw new Error(`Unsupported auth provider: ${authProvider}`);
}
