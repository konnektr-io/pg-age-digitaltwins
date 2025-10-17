import { DigitalTwinsClient } from "@azure/digital-twins-core";
import type { TokenCredential } from "@azure/core-auth";
import type {
  PipelinePolicy,
  PipelineRequest,
  PipelineResponse,
  SendRequest,
} from "@azure/core-rest-pipeline";

/**
 * Pipeline policy that rewrites URLs and adds custom headers
 */
const createCustomProxyPolicy = (
  adtHost: string,
  pathRewrite: (path: string) => string
): PipelinePolicy => {
  return {
    name: "customProxyPolicy",
    sendRequest: async (
      request: PipelineRequest,
      next: SendRequest
    ): Promise<PipelineResponse> => {
      // Add custom header
      request.headers.set("x-adt-host", adtHost);

      // Rewrite URL
      const url = new URL(request.url);
      const baseUrl = new URL(window.location.origin);
      url.host = baseUrl.host;
      url.pathname = pathRewrite(url.pathname);
      url.protocol = baseUrl.protocol;
      request.url = url.toString();

      // Pass to next policy in pipeline
      return next(request);
    },
  };
};

/** Digital Twins Clients cache object */
const digitalTwinsClients: { [adtHost: string]: DigitalTwinsClient } = {};

/**
 * Get the twins proxy path from environment or use default
 * TODO: Configure this properly in environment variables
 */
const getTwinsProxyPath = (): string => {
  return import.meta.env.VITE_TWINS_PROXY || "/api/proxy";
};

/** Digital Twins client factory that returns cached Digital Twins Client */
export const digitalTwinsClientFactory = (
  adtHost: string,
  tokenCredential: TokenCredential
): DigitalTwinsClient => {
  // `/api/digitaltwins${url.pathname}`
  if (!digitalTwinsClients[adtHost]) {
    // find all consecutive forward slashes (two or more)
    const _pathRegex = /(\/){2,}/g;
    const _pathRewrite = (path: string) =>
      `${getTwinsProxyPath()}${path}`.replace(_pathRegex, "/");

    const customPolicy = createCustomProxyPolicy(adtHost, _pathRewrite);

    digitalTwinsClients[adtHost] = new DigitalTwinsClient(
      `https://${adtHost}/`,
      tokenCredential,
      {
        allowInsecureConnection: window.location.hostname === "localhost",
        additionalPolicies: [{ policy: customPolicy, position: "perCall" }],
      }
    );
  }
  return digitalTwinsClients[adtHost];
};
