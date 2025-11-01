/**
 * Development proxy middleware for Azure Digital Twins
 *
 * This middleware dynamically routes requests to different Azure Digital Twins instances
 * based on the x-adt-host header, similar to Azure Digital Twins Explorer.
 *
 * For local development only - not used in production builds.
 */

import type { Connect } from "vite";
import { createProxyMiddleware } from "http-proxy-middleware";
import type { IncomingMessage } from "http";

/**
 * Setup proxy middleware for Vite dev server
 * Routes requests to Azure Digital Twins instances based on x-adt-host header
 */
export function setupProxy(app: Connect.Server): void {
  const proxy = createProxyMiddleware({
    target: "https://localhost/", // Will be overridden by router
    changeOrigin: true,
    secure: true,
    logger: console,
    pathFilter: "/api/proxy",
    pathRewrite: (path: string) => {
      // Remove /api/proxy prefix from the path
      return path.replace(/^\/api\/proxy/, "");
    },
    // Dynamically route based on x-adt-host header
    router: (req: IncomingMessage) => {
      const adtHost = req.headers["x-adt-host"];
      if (adtHost && typeof adtHost === "string") {
        console.log(
          `[Dev Proxy] Routing ${req.method} ${req.url} -> https://${adtHost}`
        );
        return `https://${adtHost}/`;
      }
      // Fallback (should not happen if x-adt-host is always set)
      console.warn("[Dev Proxy] No x-adt-host header found");
      return "https://localhost/";
    },
    // v3 event handlers
    on: {
      proxyReq: (proxyReq, req: any) => {
        // Log request details for debugging
        console.log(`[Dev Proxy] Request:`, {
          method: req.method,
          url: req.url,
          "content-type": req.headers["content-type"],
          "content-length": req.headers["content-length"],
          authorization: req.headers["authorization"] ? "Bearer ***" : "none",
        });

        if (proxyReq.getHeader("origin")) {
          proxyReq.removeHeader("origin");
          proxyReq.removeHeader("referer");
        }
        // Remove x-adt-host header before forwarding
        proxyReq.removeHeader("x-adt-host");
      },
      proxyRes: (proxyRes, req: any) => {
        const contentLength = proxyRes.headers["content-length"] || "unknown";
        const transferEncoding = proxyRes.headers["transfer-encoding"];
        console.log(
          `[Dev Proxy] Response: ${req.method} ${req.url} -> ${
            proxyRes.statusCode
          } (${contentLength} bytes, transfer-encoding: ${
            transferEncoding || "none"
          })`
        );

        // Log when data starts flowing
        let bytesReceived = 0;
        proxyRes.on("data", (chunk) => {
          bytesReceived += chunk.length;
        });
        proxyRes.on("end", () => {
          console.log(
            `[Dev Proxy] Response complete: ${bytesReceived} bytes received`
          );
        });
      },
      error: (err, req: any, res: any) => {
        console.error(
          `[Dev Proxy] Error: ${req.method} ${req.url}`,
          err.message
        );
        if (!res.headersSent) {
          res.writeHead(500, {
            "Content-Type": "application/json",
          });
          res.end(
            JSON.stringify({
              error: "Proxy error",
              message: err.message,
            })
          );
        }
      },
    },
  });

  app.use(proxy as Connect.NextHandleFunction);

  console.log(
    "[Dev Proxy] ✓ Azure Digital Twins proxy middleware registered at /api/proxy"
  );
}
