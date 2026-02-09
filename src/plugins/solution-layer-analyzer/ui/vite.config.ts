import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { federation } from "@module-federation/vite";
import { dependencies } from "./package.json";

export default defineConfig({
  plugins: [
    federation({
      name: "sla_plugin",
      filename: "remoteEntry.js",
      exposes: {
        "./Plugin": "./src/Plugin.tsx",
      },
      shared: {
        react: { singleton: true, requiredVersion: dependencies.react },
        "react-dom": {
          singleton: true,
          requiredVersion: dependencies["react-dom"],
        },
        "@fluentui/react-components": {
          singleton: true,
          requiredVersion: dependencies["@fluentui/react-components"],
        },
        "@fluentui/react-icons": {
          singleton: true,
          requiredVersion: dependencies["@fluentui/react-icons"],
        },
        "@ddk/host-sdk": {
          singleton: true,
          requiredVersion: dependencies["@ddk/host-sdk"],
        },
      },
      dts: false,
    }),
    react(),
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
