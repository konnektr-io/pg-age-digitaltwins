/**
 * Token credential interface compatible with Azure Digital Twins SDK
 */
interface AccessToken {
  token: string;
  expiresOnTimestamp: number;
}

interface TokenCredential {
  getToken(
    scopes: string | string[],
    options?: Record<string, unknown>
  ): Promise<AccessToken | null>;
}

/**
 * Auth0-based token credential for Azure Digital Twins SDK
 * Implements the TokenCredential interface expected by the Azure SDK
 */
export class Auth0TokenCredential implements TokenCredential {
  private getAccessTokenSilently: (options?: {
    authorizationParams?: {
      audience?: string;
      [key: string]: unknown;
    };
    [key: string]: unknown;
  }) => Promise<string>;

  constructor(
    getAccessTokenSilently: (options?: {
      authorizationParams?: {
        audience?: string;
        [key: string]: unknown;
      };
      [key: string]: unknown;
    }) => Promise<string>
  ) {
    this.getAccessTokenSilently = getAccessTokenSilently;
  }

  async getToken(
    _scopes: string | string[],
    _options?: Record<string, unknown>
  ): Promise<AccessToken | null> {
    try {
      const token = await this.getAccessTokenSilently({
        authorizationParams: {
          audience: import.meta.env.VITE_AUTH0_AUDIENCE,
        },
      });

      if (!token) {
        return null;
      }

      // Parse JWT to get expiration time
      const payload = JSON.parse(atob(token.split(".")[1]));
      const expiresOnTimestamp = payload.exp * 1000; // Convert to milliseconds

      return {
        token,
        expiresOnTimestamp,
      };
    } catch (error) {
      console.error("Failed to get Auth0 token:", error);
      return null;
    }
  }
}
