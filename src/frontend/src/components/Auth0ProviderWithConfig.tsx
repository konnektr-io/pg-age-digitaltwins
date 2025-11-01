import { Auth0Provider } from "@auth0/auth0-react";
import type { ReactNode } from "react";

interface Auth0ProviderWithConfigProps {
  children: ReactNode;
}

export function Auth0ProviderWithConfig({
  children,
}: Auth0ProviderWithConfigProps): React.ReactElement {
  const domain = import.meta.env.VITE_AUTH0_DOMAIN;
  const clientId = import.meta.env.VITE_AUTH0_CLIENT_ID;
  const audience = import.meta.env.VITE_AUTH0_AUDIENCE;
  const redirectUri = window.location.origin;

  if (!domain || !clientId || !audience) {
    console.error(
      "Auth0 configuration missing. Please check your connection settings."
    );
    return <div>Auth0 configuration error. Please contact support.</div>;
  }

  return (
    <Auth0Provider
      domain={domain}
      clientId={clientId}
      authorizationParams={{
        redirect_uri: redirectUri,
        audience: audience,
      }}
      useRefreshTokens={true}
      cacheLocation="localstorage"
    >
      {children}
    </Auth0Provider>
  );
}
