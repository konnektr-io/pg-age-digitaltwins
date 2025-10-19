import path from "path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react-swc";
import { defineConfig } from "vite";
import { VitePluginRadar } from "vite-plugin-radar";

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePluginRadar({
      gtm: [
        {
          id: process.env.VITE_GTM_ID || "",
        },
      ],
    }),
  ],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    proxy: {
      // Proxy all API requests to the backend
      "/api": {
        target: process.env.VITE_API_BASE_URL || "http://localhost:5000",
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ""),
        configure: (proxy) => {
          proxy.on("proxyReq", (proxyReq, req) => {
            // Forward x-adt-host header from frontend request
            const adtHost = req.headers["x-adt-host"];
            if (adtHost) {
              proxyReq.setHeader("x-adt-host", adtHost as string);
            }
          });
        },
      },
    },
  },
});
