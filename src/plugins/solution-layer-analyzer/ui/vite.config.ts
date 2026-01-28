import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import federation from "@originjs/vite-plugin-federation";

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: "sla_plugin",
      filename: "remoteEntry.js",
      exposes: {
        "./Plugin": "./src/Plugin.tsx",
      },
      shared: [
        "react",
        "react-dom",
        "@fluentui/react-components",
        "@fluentui/react-icons",
      ],
    }),
  ],
  build: {
    modulePreload: false,
    target: "esnext",
    minify: false,
    cssCodeSplit: false,
  },
  server: {
    port: 5174,
    strictPort: true,
  },
});
