
// Windows temporarily needs this file, https://github.com/module-federation/vite/issues/68

    import {loadShare} from "@module-federation/runtime";
    const importMap = {
      
        "@ddk/host-sdk": async () => {
          let pkg = await import("__mf__virtual/shell__prebuild___mf_0_ddk_mf_1_host_mf_2_sdk__prebuild__.js");
            return pkg;
        }
      ,
        "@fluentui/react-components": async () => {
          let pkg = await import("__mf__virtual/shell__prebuild___mf_0_fluentui_mf_1_react_mf_2_components__prebuild__.js");
            return pkg;
        }
      ,
        "@fluentui/react-icons": async () => {
          let pkg = await import("__mf__virtual/shell__prebuild___mf_0_fluentui_mf_1_react_mf_2_icons__prebuild__.js");
            return pkg;
        }
      ,
        "react": async () => {
          let pkg = await import("__mf__virtual/shell__prebuild__react__prebuild__.js");
            return pkg;
        }
      ,
        "react-dom": async () => {
          let pkg = await import("__mf__virtual/shell__prebuild__react_mf_2_dom__prebuild__.js");
            return pkg;
        }
      
    }
      const usedShared = {
      
          "@ddk/host-sdk": {
            name: "@ddk/host-sdk",
            version: "1.0.0",
            scope: ["default"],
            loaded: false,
            from: "shell",
            async get () {
              if (false) {
                throw new Error(`Shared module '${"@ddk/host-sdk"}' must be provided by host`);
              }
              usedShared["@ddk/host-sdk"].loaded = true
              const {"@ddk/host-sdk": pkgDynamicImport} = importMap
              const res = await pkgDynamicImport()
              const exportModule = {...res}
              // All npm packages pre-built by vite will be converted to esm
              Object.defineProperty(exportModule, "__esModule", {
                value: true,
                enumerable: false
              })
              return function () {
                return exportModule
              }
            },
            shareConfig: {
              singleton: true,
              requiredVersion: "workspace:*",
              
            }
          }
        ,
          "@fluentui/react-components": {
            name: "@fluentui/react-components",
            version: "9.72.10",
            scope: ["default"],
            loaded: false,
            from: "shell",
            async get () {
              if (false) {
                throw new Error(`Shared module '${"@fluentui/react-components"}' must be provided by host`);
              }
              usedShared["@fluentui/react-components"].loaded = true
              const {"@fluentui/react-components": pkgDynamicImport} = importMap
              const res = await pkgDynamicImport()
              const exportModule = {...res}
              // All npm packages pre-built by vite will be converted to esm
              Object.defineProperty(exportModule, "__esModule", {
                value: true,
                enumerable: false
              })
              return function () {
                return exportModule
              }
            },
            shareConfig: {
              singleton: true,
              requiredVersion: "^9.56.4",
              
            }
          }
        ,
          "@fluentui/react-icons": {
            name: "@fluentui/react-icons",
            version: "2.0.317",
            scope: ["default"],
            loaded: false,
            from: "shell",
            async get () {
              if (false) {
                throw new Error(`Shared module '${"@fluentui/react-icons"}' must be provided by host`);
              }
              usedShared["@fluentui/react-icons"].loaded = true
              const {"@fluentui/react-icons": pkgDynamicImport} = importMap
              const res = await pkgDynamicImport()
              const exportModule = {...res}
              // All npm packages pre-built by vite will be converted to esm
              Object.defineProperty(exportModule, "__esModule", {
                value: true,
                enumerable: false
              })
              return function () {
                return exportModule
              }
            },
            shareConfig: {
              singleton: true,
              requiredVersion: "^2.0.272",
              
            }
          }
        ,
          "react": {
            name: "react",
            version: "18.3.1",
            scope: ["default"],
            loaded: false,
            from: "shell",
            async get () {
              if (false) {
                throw new Error(`Shared module '${"react"}' must be provided by host`);
              }
              usedShared["react"].loaded = true
              const {"react": pkgDynamicImport} = importMap
              const res = await pkgDynamicImport()
              const exportModule = {...res}
              // All npm packages pre-built by vite will be converted to esm
              Object.defineProperty(exportModule, "__esModule", {
                value: true,
                enumerable: false
              })
              return function () {
                return exportModule
              }
            },
            shareConfig: {
              singleton: true,
              requiredVersion: "^18.3.1",
              
            }
          }
        ,
          "react-dom": {
            name: "react-dom",
            version: "18.3.1",
            scope: ["default"],
            loaded: false,
            from: "shell",
            async get () {
              if (false) {
                throw new Error(`Shared module '${"react-dom"}' must be provided by host`);
              }
              usedShared["react-dom"].loaded = true
              const {"react-dom": pkgDynamicImport} = importMap
              const res = await pkgDynamicImport()
              const exportModule = {...res}
              // All npm packages pre-built by vite will be converted to esm
              Object.defineProperty(exportModule, "__esModule", {
                value: true,
                enumerable: false
              })
              return function () {
                return exportModule
              }
            },
            shareConfig: {
              singleton: true,
              requiredVersion: "^18.3.1",
              
            }
          }
        
    }
      const usedRemotes = [
      ]
      export {
        usedShared,
        usedRemotes
      }
      