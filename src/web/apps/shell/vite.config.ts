import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { federation } from "@module-federation/vite";
import { dependencies } from "./package.json";

export default defineConfig({
  plugins: [
    federation({
      name: "shell",
      shared: {
        react: {
          singleton: true,
          requiredVersion: dependencies.react,
        },
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
    target: "esnext",
    minify: false,
    cssCodeSplit: false,
    rollupOptions: {
      output: {
        format: "esm",
      },
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    cors: true,
    headers: {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, PUT, DELETE, PATCH, OPTIONS",
      "Access-Control-Allow-Headers":
        "X-Requested-With, content-type, Authorization",
    },
  },
});
