import { useAuth0 } from "@auth0/auth0-react";
import { DigitalTwinsClient } from "@azure/digital-twins-core";
import { Auth0TokenCredential } from "@/services/Auth0TokenCredential";
import { digitalTwinsClientFactory } from "@/services/digitalTwinsClientFactory";
import { useEnvironmentStore } from "@/stores/environmentStore";
import { useMemo } from "react";

/**
 * Hook that provides an authenticated DigitalTwinsClient using Auth0.
 * This hook should be used in components that need to interact with the Digital Twins API.
 */
export function useDigitalTwinsClient(): DigitalTwinsClient | null {
  const { getAccessTokenSilently, isAuthenticated } = useAuth0();
  const currentEnvironment = useEnvironmentStore((state) =>
    state.getCurrentEnvironment()
  );

  const client = useMemo(() => {
    if (!isAuthenticated || !currentEnvironment) {
      return null;
    }

    const tokenCredential = new Auth0TokenCredential(getAccessTokenSilently);
    return digitalTwinsClientFactory(
      currentEnvironment.id,
      currentEnvironment.adtHost,
      tokenCredential
    );
  }, [isAuthenticated, currentEnvironment, getAccessTokenSilently]);

  return client;
}
