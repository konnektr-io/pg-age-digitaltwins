import path from "path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react-swc";
import { defineConfig } from "vite";
import { VitePluginRadar } from "vite-plugin-radar";
import { setupProxy } from "./src/setupProxy";

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
    // Custom plugin to register proxy middleware
    {
      name: "configure-adt-proxy",
      configureServer(server) {
        // Register custom proxy middleware for Azure Digital Twins
        setupProxy(server.middlewares);
      },
    },
  ],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
});
